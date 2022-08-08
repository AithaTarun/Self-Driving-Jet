using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cinemachine;
using Random = UnityEngine.Random;

namespace Aircraft
{
    public class AircraftArea : MonoBehaviour
    {
        [Tooltip("The path the race will take")]
        public CinemachineSmoothPath racePath;

        [Tooltip("The prefab to use for checkpoints")]
        public GameObject checkpointPrefab;

        [Tooltip("The prefab to use for start/end checkpoint")]
        public GameObject finishCheckpointPrefab;

        [Tooltip("If true, enable training mode")]
        public bool trainingMode;
        
        public  List<AircraftAgent> AircraftAgents { get; private set; }
        
        public  List<GameObject> Checkpoints { get; private set; }

        /// <summary>
        /// Actions to perform when script wakes up
        /// </summary>
        private void Awake()
        {
            if (AircraftAgents == null)
            {
                FindAircraftAgents();
            }
        }

        /// <summary>
        /// Finds Aircraft Agents in the area
        /// </summary>
        private void FindAircraftAgents()
        {
            // Find all aircraft agents in the area
            AircraftAgents = transform.GetComponentsInChildren<AircraftAgent>().ToList();

            Debug.Assert(AircraftAgents.Count > 0, "No Aircraft Agents found");
        }

        /// <summary>
        /// Set up the area
        /// </summary>
        private void Start()
        {
            if (Checkpoints == null)
            {
                CreateCheckpoints();   
            }
        }

        /// <summary>
        /// Creates the checkpoints
        /// </summary>
        private void CreateCheckpoints()
        {
            // Create checkpoints along the race path
            Debug.Assert(racePath != null, "Race path was not set");

            Checkpoints = new List<GameObject>();

            int numCheckpoints = (int)racePath.MaxUnit(CinemachinePathBase.PositionUnits.PathUnits);
            for (int i = 0; i < numCheckpoints; i++)
            {
                // Instantiate either a checkpoint or finish line checkpoint
                GameObject checkpoint = i == numCheckpoints - 1
                    ? Instantiate<GameObject>(finishCheckpointPrefab)
                    : Instantiate<GameObject>(checkpointPrefab);

                // Set the parent, position, and rotation
                checkpoint.transform.SetParent(racePath.transform);
                checkpoint.transform.localPosition = racePath.m_Waypoints[i].position;
                checkpoint.transform.rotation =
                    racePath.EvaluateOrientationAtUnit(i, CinemachinePathBase.PositionUnits.PathUnits);

                // Add checkpoint to the list
                Checkpoints.Add(checkpoint);
            }
        }

        /// <summary>
        /// Resets the position of an agent using its current NextCheckpointIndex, unless randomize is true,
        /// then it will pick a random checkpoint  
        /// </summary>
        /// <param name="agent">The agent to reset</param>
        /// <param name="randomize">If true, will pick a new NextCheckpointIndex before reset</param>
        public void ResetAgentPosition(AircraftAgent agent, bool randomize = false)
        {
            if (AircraftAgents == null)
            {
                FindAircraftAgents();
            }
            
            if (Checkpoints == null)
            {
                CreateCheckpoints();   
            }
            
            if (randomize)
            {
                // Pick a new checkpoint at random
                agent.NextCheckpointIndex = Random.Range(0, Checkpoints.Count);
            }
            
            // Set start position to th previous checkpoint
            int previousCheckpointIndex = agent.NextCheckpointIndex - 1;
            if (previousCheckpointIndex == -1)
            {
                previousCheckpointIndex = Checkpoints.Count - 1;
            }

            float startPosition =
                racePath.FromPathNativeUnits(previousCheckpointIndex, CinemachinePathBase.PositionUnits.PathUnits);
            
            // Convert the position on the race path to a position in 3D space
            Vector3 basePosition = racePath.EvaluatePosition(startPosition);
            
            // Get the orientation at that position on the race path
            Quaternion orientation = racePath.EvaluateOrientation(startPosition);
            
            // Calculate a horizontal offset so that agents are spread out
            Vector3 positionOffset = Vector3.right * (AircraftAgents.IndexOf(agent) - AircraftAgents.Count / 2f) * Random.Range(9f, 10f);
            
            // Set teh aircraft position and rotation
            agent.transform.position = basePosition + orientation * positionOffset;
            agent.transform.rotation = orientation;
        }
    }
}

