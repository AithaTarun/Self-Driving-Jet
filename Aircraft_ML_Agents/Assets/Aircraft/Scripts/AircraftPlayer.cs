using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aircraft
{
    public class AircraftPlayer : AircraftAgent
    {
        [Header("Input Bindings")] 
        public InputAction pitchInput;
        public InputAction yawInput;
        public InputAction boostInput;
        public InputAction pauseInput;

        /// <summary>
        /// Calls base initialize and initializes inputs
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            
            pitchInput.Enable();
            yawInput.Enable();
            boostInput.Enable();
            pauseInput.Enable();
        }
        
        /// <summary>
        /// Reads player input and convert it into vector of actions.
        /// </summary>
        /// <param name="actionsOut">An array of floats for OnActionReceived to use</param>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            // Pitch : 1 == up; 0 = None; -1= down
            float pitchValue = pitchInput.ReadValue<float>();
            
            // Yaw : 1 == turn right; 0 = None; -1 = turn left
            float yawValue = yawInput.ReadValue<float>();
            
            // Boost : 1 == boost; 0 = No boost
            float boostValue = boostInput.ReadValue<float>();
            
            // Convert -1 (down) to discrete value 2
            if (pitchValue == -1f)
            {
                pitchValue = 2f;
            }
            
            // Convert -1 (turn left) to discrete value 2
            if (yawValue == -1f)
            {
                yawValue = 2f;
            }

            actionsOut.DiscreteActions.Array[0] = (int) pitchValue;
            actionsOut.DiscreteActions.Array[1] = (int) yawValue;
            actionsOut.DiscreteActions.Array[2] = (int) boostValue;
        }

        /// <summary>
        /// Cleans up the inputs when destroyed
        /// </summary>
        private void OnDestroy()
        {
            pitchInput.Disable();
            yawInput.Disable();
            boostInput.Disable();
            pauseInput.Disable();
        }
    }
}
