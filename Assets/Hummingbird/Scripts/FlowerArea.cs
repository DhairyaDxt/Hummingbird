using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// It manages a collection of flowers in a specific area
public class FlowerArea : MonoBehaviour
{
    // The diameter of the area where the agent and flowers can be found
    public const float AreaDiameter = 20f;

    // The list of all flower plants in this flower area
    private List<GameObject> flowerPlants;

    // A lookup dictionary for looking up a flower from a nectar collider
    private Dictionary<Collider, Flower> nectarFlowerDictionary;

    // List of all flowers in this area
    public List<Flower> Flowers { get; private set; }

    [Header("Resource Control")]
    [Range(1, 20)]
    public int activeFlowerCount = 20;

    [Header("Nectar Restoration")]
    [Tooltip("Seconds after a flower is depleted before nectar is restored in another random flower")]
    public float restoreDelay = 40f;

    public bool restoreEnabled = true;

    // Reset the flowers in this area
    public void ResetFlowers()
    {
        // Cancel any pending restorations from the previous episode
        StopAllCoroutines();

        foreach (GameObject flowerPlant in flowerPlants)
        {
            float xRotation = UnityEngine.Random.Range(-5f, 5f);
            float yRotation = UnityEngine.Random.Range(-180f, 180f);
            float zRotation = UnityEngine.Random.Range(-5f, 5f);

            flowerPlant.transform.localRotation =
                Quaternion.Euler(xRotation, yRotation, zRotation);
        }

        foreach (Flower flower in Flowers)
        {
            flower.ResetFlower();
        }

        foreach (Flower flower in Flowers)
        {
            flower.SetAvailable(false);
        }

        List<int> availableIndices = new List<int>();
        for (int i = 0; i < Flowers.Count; i++)
        {
            availableIndices.Add(i);
        }

        // Shuffle
        for (int i = 0; i < availableIndices.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, availableIndices.Count);
            int temp = availableIndices[i];
            availableIndices[i] = availableIndices[randomIndex];
            availableIndices[randomIndex] = temp;
        }

        int flowersToActivate = Mathf.Clamp(activeFlowerCount, 0, Flowers.Count);
        for (int i = 0; i < flowersToActivate; i++)
        {
            Flowers[availableIndices[i]].SetAvailable(true);
        }
    }

    public Flower GetFlowerFromNectar(Collider collider)
    {
        return nectarFlowerDictionary[collider];
    }

    private void Awake()
    {
        flowerPlants = new List<GameObject>();
        nectarFlowerDictionary = new Dictionary<Collider, Flower>();
        Flowers = new List<Flower>();
    }

    private void Start()
    {
        FindChildFlowers(transform);
        ResetFlowers();
    }

    // Called by a flower when its nectar is used up
    private void HandleFlowerDepleted(Flower depletedFlower)
    {
        if (!restoreEnabled || !isActiveAndEnabled)
            return;

        StartCoroutine(RestoreNectarAfterDelay(depletedFlower));
    }

    private IEnumerator RestoreNectarAfterDelay(Flower sourceFlower)
    {
        yield return new WaitForSeconds(restoreDelay);

        // Candidates: every flower that currently has no nectar, except the one just emptied
        List<Flower> candidates = new List<Flower>();
        foreach (Flower flower in Flowers)
        {
            if (flower != sourceFlower && !flower.HasNector)
            {
                candidates.Add(flower);
            }
        }

        if (candidates.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, candidates.Count);
            Flower target = candidates[index];
            target.SetAvailable(true);

            if (ResourceExperimentManager.Instance != null)
            {
                ResourceExperimentManager.Instance.RegisterFlowerRestored(target);
            }
        }
        // If every other flower already has nectar, nothing is restored
        // (the total number of active flowers never exceeds activeFlowerCount).
    }

    private void FindChildFlowers(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.CompareTag("flower_plant"))
            {
                flowerPlants.Add(child.gameObject);
                FindChildFlowers(child);
            }
            else
            {
                Flower flower = child.GetComponent<Flower>();

                if (flower != null)
                {
                    Flowers.Add(flower);
                    nectarFlowerDictionary.Add(flower.nectarCollider, flower);

                    // Listen for depletion so nectar can be restored elsewhere
                    flower.OnDepleted += HandleFlowerDepleted;
                }
                else
                {
                    FindChildFlowers(child);
                }
            }
        }
    }
}
