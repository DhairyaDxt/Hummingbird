using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.VisualScripting;

// humming bird machine learning agent
public class HummingBirdAgent : Agent
{

    [Tooltip("Force to apply when moving")]
    public float moveForce = 2f;
    [Tooltip("Speed to pitch up or down")]
    public float pitchSpeed = 100f;
    [Tooltip("Speed to rotate around the up axis")]
    public float yawSpeed = 100f;
    [Tooltip("Transform of the beak tip, used for feeding")]
    public Transform beakTip;
    [Tooltip("The agents camera")]
    public Camera agentCamera;
    [Tooltip("Whether this is training mode or gameplay mode")]
    public bool trainingMode;

    // THE RIGID BODY OF THE AGENT
    new private Rigidbody rigidbody;

    // the flower area
    private FlowerArea flowerArea;

    // neraest flower to the agent
    private Flower nearestFlower;

    // allows for smoother pitch changes
    private float smoothPitchChange = 0f;

    // allows for smoother yaw changes
    private float smoothYawChange = 0f;

    // maximum angle the beak can pitch up or down
    private const float MaxPitchAngle = 80f;

    // maximum distance from the beak tip to the nectar for a successful feed
    private const float BeakTipRadius = 0.008f;

    // whether the agent is frozen ( intentionally not moving ) or not, used for resetting the agent
    private bool frozen = false;

    // the amount of nectar obtained by the agent
    public float NectarObtained { get; private set; }

    // initialization of the agent
    public override void Initialize()
    {
        rigidbody = GetComponent<Rigidbody>();
        flowerArea = GetComponentInParent<FlowerArea>();

        // if not in training mode, set the behavior type to inference only
        if (!trainingMode) MaxStep = 0;

    }

    // reset the agent at the beginning of each episode
    public override void OnEpisodeBegin()
    {
        if (trainingMode)
        {
            // only reset flowers in traning mode
            flowerArea.ResetFlowers();
        }

        // reset nectar obtained
        NectarObtained = 0f;

        // zero out the velocity so that the agent isn't moving when the next episode starts
        rigidbody.linearVelocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;

        // default to spawing in front of flowers
        bool inFrontOfFlowers = true;
        if (trainingMode)
        {
            // spawn in front of flowers 50% of the time during training, and behind flowers the other 50% of the time
            inFrontOfFlowers = UnityEngine.Random.value > 0.5f;
        }

        // move the agent to a new random position and orientation
        MoveToSafeRandomPosition(inFrontOfFlowers);

        // recalculate the nearest flower after moving to a new position
        UpdateNearestFlower();

    }

    // called when action is received or the neural network
    /// <summary>
    /// 
    /// </summary>
    /// <param name="vectorAction"></param>
    public override void OnActionReceived(ActionBuffers actions)
    {
        var vectorAction = actions.ContinuousActions;

        // donit take action if frozen
        if (frozen) return;

        //calculate movemnt vector
        Vector3 move = new Vector3(vectorAction[0], vectorAction[1], vectorAction[2]);
        rigidbody.AddForce(move * moveForce);

        // set the current rotation
        Vector3 rotationVector = transform.rotation.eulerAngles;

        // calculates pitch and yawn rotation
        float pitchChange = vectorAction[3];
        float yawnChnage = vectorAction[4];

        // calculates smooth rotation changes
        smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
        smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawnChnage, 2f * Time.fixedDeltaTime);

        //calculate new pitch and you based on smoothed values
        float pitch = rotationVector.x + smoothPitchChange * Time.fixedDeltaTime * pitchSpeed;
        if (pitch > 180f) pitch -= 360f;
        pitch = Mathf.Clamp(pitch, -MaxPitchAngle, MaxPitchAngle);

        float yaw = rotationVector.y + smoothYawChange * Time.fixedDeltaTime * yawSpeed;

        //apply the new rotation
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    /// <summary>
    /// collect vector observation from the enviorment
    /// </summary>
    /// <param name="sensor"></param>
    public override void CollectObservations(VectorSensor sensor)
    {
        // if nearest flower is null, observe an wmpty area and return early
        if (nearestFlower == null)
        {
            sensor.AddObservation(new float[10]);
            return;
        }

        //observe agents local rotation
        sensor.AddObservation(transform.localRotation.normalized);
        // get a Vector from beak tip to the nearest flower
        Vector3 toFlower = nearestFlower.NectarCenterPosition - beakTip.position;

        // observe a normalized vector pointing to the neearest flower
        sensor.AddObservation(toFlower.normalized);

        // observe a dot product that indicated wether the beaktip is front of the flowers
        // (+) means front of the the flowers (-) back of the flowers
        sensor.AddObservation(Vector3.Dot(toFlower.normalized, -nearestFlower.FlowerUpVector.normalized));

        // observe dot product which indicates wether the beak is pointing towards the flowerr
        // (+1) means beak points directly at the flower (-1) pointing directly away
        sensor.AddObservation(Vector3.Dot(beakTip.forward.normalized, -nearestFlower.FlowerUpVector.normalized));

        //observe the relatuive distance from the beaktip to the flower
        sensor.AddObservation(toFlower.magnitude / FlowerArea.AreaDiameter);

        // 10 total observations
    }

    /// <summary>
    /// when behaviour type is set to heuristic only on the agent behaviour parameters
    /// </summary>
    /// <param name="actionsOut"></param>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // create placeholders for all movement/turning
        Vector3 forward = Vector3.zero;
        Vector3 Left = Vector3.zero;
        Vector3 up = Vector3.zero;
        float pitch = 0f;
        float yaw = 0f;

        var continuousActions = actionsOut.ContinuousActions;

        // all values should be btw -1 and +1
        if (Input.GetKey(KeyCode.W)) forward = transform.forward;
        else if (Input.GetKey(KeyCode.S)) forward = -transform.forward;

        //leFT RIGHT
        if (Input.GetKey(KeyCode.A)) Left = -transform.right;
        else if (Input.GetKey(KeyCode.D)) Left = transform.right;

        // up and down
        if (Input.GetKey(KeyCode.E)) up = transform.up;
        else if (Input.GetKey(KeyCode.C)) up = -transform.up;

        // pitch up and down
        if (Input.GetKey(KeyCode.UpArrow)) pitch = -1f;
        else if (Input.GetKey(KeyCode.DownArrow)) pitch = 1f;

        // turn left right
        if (Input.GetKey(KeyCode.LeftArrow)) yaw = -1f;
        else if (Input.GetKey(KeyCode.RightArrow)) yaw = 1f;

        // combine the movement vector and normalize
        Vector3 combined = (forward + Left + up).normalized;

        // add the 3 movement values, pitch, yaw, to the actionOut array
        continuousActions[0] = combined.x;
        continuousActions[1] = combined.y;
        continuousActions[2] = combined.z;
        continuousActions[3] = pitch;
        continuousActions[4] = yaw;
    }

    /// <summary>
    /// prevent the agent from moving and taking actions
    /// </summary>
    public void FreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeeze/Unfreeze not supported in training");
        frozen = true;
        rigidbody.Sleep();
    }

    /// <summary>
    /// resume agent movement and actions
    /// </summary>
    public void UnFreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeeze/Unfreeze not supported in training");
        frozen = false;
        rigidbody.WakeUp();
    }



    // move the agent to a new random position and orientation
    private void MoveToSafeRandomPosition(bool inFrontOfFlower)
    {
        bool safePositionFound = false;
        int attemptsRemaining = 100;// prevent infinite loop if something goes wrong
        Vector3 potentialPosition = Vector3.zero;
        Quaternion potentialRotation = Quaternion.identity;

        while (!safePositionFound && attemptsRemaining > 0)
        {
            attemptsRemaining--;
            if (inFrontOfFlower)
            {
                // GET A RANDOM FLOWER FROM THE FLOWER AREA
                Flower randomFlower = flowerArea.Flowers[UnityEngine.Random.Range(0, flowerArea.Flowers.Count)];

                // position 10 to 20 cm in front of the flower
                float distanceFromFlower = UnityEngine.Random.Range(0.1f, 0.2f);
                potentialPosition = randomFlower.transform.position + randomFlower.transform.forward * distanceFromFlower;

                // point beak at flower 
                Vector3 toFlower = randomFlower.transform.position - beakTip.position;
                potentialRotation = Quaternion.LookRotation(toFlower, Vector3.up);
            }
            else
            {
                // pick a random height from the ground
                float height = UnityEngine.Random.Range(1.2f, 2.5f);

                // pick a random radius from the center of the flower area
                float radius = UnityEngine.Random.Range(2f, 7f);

                // pick a random direction rotates around the y axis
                Quaternion direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-180f, 180f), 0f);

                // combine the height, radius, and direction to get a potential position
                potentialPosition = flowerArea.transform.position + Vector3.up * height + direction * Vector3.forward * radius;

                // choose a random rotation
                float pitch = UnityEngine.Random.Range(-60f, 60f);
                float yaw = UnityEngine.Random.Range(-180f, 180f);
                potentialRotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            //  check if the agent will collide with anything
            Collider[] colliders = Physics.OverlapSphere(potentialPosition, 0.05f);

            // Safe position found if there are no colliders are overlapped
            safePositionFound = colliders.Length == 0;
        }

        Debug.Assert(safePositionFound, "Could not find a safe position to spawn");
        transform.position = potentialPosition;
        transform.rotation = potentialRotation;
    }

    // update the nearest flower to the agent
    private void UpdateNearestFlower()
    {
        foreach (Flower flower in flowerArea.Flowers)
        {
            if (nearestFlower == null && flower.HasNector)
            {
                // no current nearest flower and this flower has nectar
                nearestFlower = flower;
            }
            else if (flower.HasNector)
            {
                // calculate distance to this flower and distance to the current nearest flower
                float distanceToFlower = Vector3.Distance(flower.transform.position, beakTip.position);
                float distanceCurrentNearestFlower = Vector3.Distance(nearestFlower.transform.position, beakTip.position);

                // if current nearest flower is empty or this flower is clear , upadte the nearest flower
                if (!nearestFlower.HasNector || distanceToFlower < distanceCurrentNearestFlower)
                {
                    nearestFlower = flower;
                }
            }
        }
    }

    // called agents collider enters a trigger collider
    private void OnTriggerEnter(Collider other)
    {
        TriggerEnterOrStay(other);

    }

    // called agents collider stays in a trigger collider
    private void OnTriggerStay(Collider other)
    {
        TriggerEnterOrStay(other);

    }

    /// <summary>
    ///  handles when the agents collider enters or stay in a trigger collider
    /// </summary>
    /// <param name="collider"></param>
    private void TriggerEnterOrStay(Collider collider)
    {
        // check if agents is colliding with nectar
        if (collider.CompareTag("nectar"))
        {
            Vector3 closestPointToBeakTip = collider.ClosestPoint(beakTip.position);

            // check if the closest collision point is close to the beak tip
            // note a collision with anything but the beak tip should not count
            if (Vector3.Distance(beakTip.position, closestPointToBeakTip) < BeakTipRadius)
            {
                //look up the flower for this nectar collider
                Flower flower = flowerArea.GetFlowerFromNectar(collider);

                //attempts to take .01 nectar
                //note this is per fixed timestep, meaning it happens every .02 seconds or 50x per second
                float nectarReceived = flower.Feed(.01f);

                //keep track of nectar obtained
                NectarObtained += nectarReceived;
                if (ResourceExperimentManager.Instance != null)
                {
                    ResourceExperimentManager.Instance.RegisterNectarCollected(nectarReceived);
                }

                if (trainingMode)
                {
                    // calculate reward for getting nectar
                    float bonus = .02f * Mathf.Clamp01(Vector3.Dot(transform.forward.normalized, -nearestFlower.FlowerUpVector.normalized));
                    AddReward(.01f + bonus);
                }

                // if flower is empty , update the nearset flower
                if (!flower.HasNector)
                {
                    if (ResourceExperimentManager.Instance != null)
                    {
                        ResourceExperimentManager.Instance.RegisterFlowerDepleted(flower);
                    }

                    UpdateNearestFlower();
                }

            }
        }
    }

    /// <summary>
    /// called when the agent collides with something solid
    /// </summary>
    /// <param name="collision"></param>
    private void OnCollisionEnter(Collision collision)
    {
        if (trainingMode && collision.collider.CompareTag("boundary"))
        {
            //collide with the area boundary , give negative rewards
            AddReward(-5f);
        }
    }

    /// <summary>
    /// called every frame
    /// </summary>
    private void Update()
    {
        // draw a line from the beak to the nearest flower
        if (nearestFlower != null)
            Debug.DrawLine(beakTip.position, nearestFlower.NectarCenterPosition, Color.green);

    }

    /// <summary>
    /// called every .02 seconds
    /// </summary>
    private void FixedUpdate()
    {
        if (nearestFlower != null && !nearestFlower.HasNector)
            UpdateNearestFlower();
    }
}
