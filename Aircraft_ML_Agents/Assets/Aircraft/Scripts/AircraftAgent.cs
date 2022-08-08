using System;
using System.Collections;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Aircraft
{
    public class AircraftAgent : Agent
    {
        public int NextCheckpointIndex { get; set; }

        [Header("Movement Parameters")] 
        private static float normalThrust = 100000f;
        private static float boostThrust = 100000f;
        // private static float boostThrust = 150000f;
        
        public float thrust = normalThrust; // To push plane forward (z-axis)
        public float pitchSpeed = 100f; // How much we rotate around the x-axis 
        public float yawSpeed = 100f; // How much we rotate around the y-axis
        public float rollSpeed = 100f; // Rotation
        public float boostMultiplier = 2f; // Extra force to add when airplane is boosting

        [Header("Explosion Stuff")] 
        [Tooltip("The aircraft mesh hat will disappear on explosion")]
        public GameObject meshObject;

        [Tooltip("The game object of the explosion particle effect")]
        public GameObject explosionEffect;

        [Header("Training")]
        [Tooltip("Number of steps to time out after in training")]
        public int stepTimeout = 300;
        
        // Components to keep track of
        private AircraftArea area;
        new private Rigidbody rigidbody; // Body of plane
        private TrailRenderer trail; // Smoke when plane is boosting
        
        // When the next step timeout will be during training
        private float nextStepTimout;
        
        // Whether the aircraft is frozen (intentionally not flying)
        private bool frozen = false;
        
        // Controls
        private float pitchChange = 0f;
        private float smoothPitchChange = 0f;
        private float maxPitchAngle = 45f;
        private float yawChange = 0f;
        private float smoothYawChange = 0f;
        private float rollChange = 0f;
        private float smoothRollChange = 0f;
        private float maxRollAngle = 45f;
        private bool boost = false;

        /// <summary>
        /// Called when the agent is first initialized
        /// </summary>
        public override void Initialize()
        {
            area = GetComponentInParent<AircraftArea>();
            rigidbody = GetComponent<Rigidbody>();
            trail = GetComponent <TrailRenderer>();
            
            // Override the max step set in the inspector
            // Max 5000 steps if training, infinite steps if racing
            MaxStep = area.trainingMode ? 5000 : 0;
        }

        /// <summary>
        /// Called when new episode begins.
        /// </summary>
        public override void OnEpisodeBegin()
        {
            // Reset the velocity, position, and orientation
            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            trail.emitting = false;
            area.ResetAgentPosition(agent : this, randomize : area.trainingMode);
            
            // Update the step timout if training
            if (area.trainingMode)
            {
                nextStepTimout = StepCount + stepTimeout;
            }
        }

        /// <summary>
        /// Read action inputs from actions
        /// </summary>
        /// <param name="actions">The Chosen actions</param>
        public override void OnActionReceived(ActionBuffers actions)
        {
            if (frozen)
            {
                return;
            }
            
            // Read values for pitch, yaw, and boosting
            
            pitchChange = actions.DiscreteActions[0]; // Up or None
            if (pitchChange == 2)
            {
                pitchChange = -1f; // Down
            }

            yawChange = actions.DiscreteActions[1]; // Turn right or None
            if (yawChange == 2)
            {
                yawChange = -1f; // Turn left
            }
            
            // Read value for boost and enable/disable trail renderer
            boost = actions.DiscreteActions[2] == 1;
            if (boost && !trail.emitting)
            {
                trail.Clear();
            }

            // if (boost)
            // {
            //     thrust = boostThrust;
            // }
            // else
            // {
            //     thrust = normalThrust;
            // }
            
            trail.emitting = boost;

            ProcessMovement();

            if (area.trainingMode)
            {
                // Small negative reward every step, encourages to take actions
                AddReward(-1f / MaxStep);
                
                // Make sure we haven't run out of time
                if (StepCount > nextStepTimout)
                {
                    AddReward(-0.5f);
                    EndEpisode(); // Start over
                }

                // Decrease checkpoint radius to get reward and train the agent correctly.
                Vector3 localCheckpointDirection = VectorToNextCheckpoint();
                if (localCheckpointDirection.magnitude <Academy.Instance.EnvironmentParameters.GetWithDefault("checkpoint_radius", 0f))
                {
                    GotCheckpoint();
                }
            }
        }

        /// <summary>
        /// Collects observations used by agents to make decisions
        /// </summary>
        /// <param name="sensor">THe vector</param>
        public override void CollectObservations(VectorSensor sensor)
        {
            // Observe aircraft velocity (1 Vector3 = 3 values)
            sensor.AddObservation(transform.InverseTransformDirection(rigidbody.velocity));
            
            // Where is the next checkpoint ( 1 Vector3 = 3 values)
            sensor.AddObservation(VectorToNextCheckpoint());
            
            // Orientation of the next checkpoint (1 Vector3 = 3 values)
            Vector3 nextCheckpointForward = area.Checkpoints[NextCheckpointIndex].transform.forward;
            sensor.AddObservation(transform.InverseTransformDirection(nextCheckpointForward));
            
            // Total observations = 3 + 3 + 3 = 9
        }

        /// <summary>
        /// In this project we only expect Heuristic to be used on aircraft player
        /// </summary>
        /// <param name="actionsOut">Enpty array</param>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            Debug.LogError("Heuristic() was called on " + gameObject.name + " Make sure only the Aircraft is set to Behavior Type : Heuristic Only.");
        }

        /// <summary>
        /// Prevent the agent from moving and taking actions
        /// </summary>
        public void FreezeAgent()
        {
            Debug.Assert(area.trainingMode == false, "Freeze/Thaw is not supported in training");
            
            frozen = true;
            rigidbody.Sleep(); // Physics stops
            trail.emitting = false;
        }

        /// <summary>
        /// Resume agent movements and actions
        /// </summary>
        public void ThawAgent()
        {
            Debug.Assert(area.trainingMode == false, "Freeze/Thaw is not supported in training");
            
            frozen = false;
            rigidbody.WakeUp(); // Physics starts to act
        }

        /// <summary>
        /// Gets a vector to the next checkpoint the agent needs to fly through 
        /// </summary>
        /// <returns>A local space vector</returns>
        private Vector3 VectorToNextCheckpoint()
        {
            Vector3 nextCheckpointDirection = area.Checkpoints[NextCheckpointIndex].transform.position - transform.position;
            Vector3 localCheckpointDirection = transform.InverseTransformDirection(nextCheckpointDirection);

            return localCheckpointDirection;
        }

        /// <summary>
        /// Called when the agent files through the correct checkpoint
        /// </summary>
        private void GotCheckpoint()
        {
            // Next checkpoint reached, update
            NextCheckpointIndex = (NextCheckpointIndex + 1) % area.Checkpoints.Count;

            if (area.trainingMode)
            {
                AddReward(0.5f);
                nextStepTimout = StepCount + stepTimeout;
            }
        }

        /// <summary>
        /// Calculate and apply movement
        /// </summary>
        private void ProcessMovement()
        {
            // Calculate boost
            float boostModifier = boost ? boostMultiplier : 1f;
            
            // Apply forward thrust
            rigidbody.AddForce(transform.forward * thrust * boostMultiplier, ForceMode.Force);
            
            // Getting the current rotation
            Vector3 currentRotation = transform.rotation.eulerAngles;
            
            // Calculate the roll angle (between -180 and 180)
            float rollAngle = currentRotation.z > 180f ? currentRotation.z - 360f : currentRotation.z;
            if (yawChange == 0f)
            {
                // Not turning, smoothly roll toward center
                rollChange = -rollAngle / maxRollAngle;
            }
            else
            {
                // Turning, roll in opposite direction of turn
                rollChange = -yawChange;
            }
            
            // Calculate smooth deltas
            smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
            smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawChange, 2f * Time.fixedDeltaTime);
            smoothRollChange = Mathf.MoveTowards(smoothRollChange, rollChange, 2f * Time.fixedDeltaTime);
            
            // Calculate new pitch, yaw and roll. Clamp pitch and roll.
            float pitch = currentRotation.x + smoothPitchChange * Time.fixedDeltaTime * pitchSpeed;
            if (pitch > 180f)
            {
                pitch -= 360f;
            }
            pitch = Mathf.Clamp(pitch, -maxPitchAngle, maxPitchAngle);

            float yaw = currentRotation.y + smoothYawChange * Time.fixedDeltaTime * yawSpeed;

            float roll = currentRotation.z + smoothRollChange * Time.fixedDeltaTime * rollSpeed;
            if (roll > 180f)
            {
                roll -= 360f;
            }
            roll = Mathf.Clamp(roll, -maxRollAngle, maxRollAngle);
            
            // Set the new rotation
            transform.rotation = Quaternion.Euler(pitch, yaw, roll);
        }

        /// <summary>
        /// React to entering a trigger
        /// </summary>
        /// <param name="other">The collider entered</param>
        private void OnTriggerEnter(Collider other)
        {
            if (other.transform.CompareTag("checkpoint") && other.gameObject == area.Checkpoints[NextCheckpointIndex])
            {
                GotCheckpoint();
            }
        }

        /// <summary>
        /// React to collisions
        /// </summary>
        /// <param name="collision">Collision info</param>
        /// <exception cref="NotImplementedException"></exception>
        private void OnCollisionEnter(Collision collision)
        {
            if (!collision.transform.CompareTag("agent"))
            {
                // We hit something that was not another agent
                if (area.trainingMode)
                {
                    AddReward(-1f);
                    EndEpisode();
                }
                else
                {
                    StartCoroutine(ExplosionReset());
                }
            }
        }

            /// <summary>
            /// Resets the aircraft to the most recent complete checkpoint
            /// </summary>
            /// <returns>Yield return</returns>
        private IEnumerator ExplosionReset()
        {
            FreezeAgent();
            
            // Disable aircraft msh object, enable explosion
            meshObject.SetActive(false);
            explosionEffect.SetActive(true);
            yield return new WaitForSeconds(2f);
            
            // Disable explosion, re-enable aircraft mesh
            meshObject.SetActive(true);
            explosionEffect.SetActive(false);
            
            // Reset position
            area.ResetAgentPosition(agent : this);
            yield return new WaitForSeconds(1f);
            
            ThawAgent();
        }
    }
}
