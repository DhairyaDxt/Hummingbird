using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class ResourceExperimentManager : MonoBehaviour
{
    public static ResourceExperimentManager Instance;

    [Header("Experiment Settings")]
    public int numberOfBirds = 5;
    public int activeFlowerCount = 20;
    public string experimentID = "5B_20F";
    public FlowerArea experimentalFlowerArea;

    [Header("Runtime Statistics")]
    public float totalNectarCollected = 0f;
    public int flowersDepleted = 0;
    public int flowersRestored = 0;

    [Header("Crowding Statistics")]
    [Range(0.1f, 2f)]
    public float crowdingDistance = 0.5f;

    public float totalCrowdingPairTime = 0f;
    public int crowdingEvents = 0;
    public int maxSimultaneousCrowdedPairs = 0;

    private float experimentTime;
    private bool resultsPrinted = false;

    private HashSet<Flower> depletedFlowers =
        new HashSet<Flower>();

    private HashSet<string> activeCrowdingPairs =
        new HashSet<string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        if (experimentalFlowerArea != null)
        {
            experimentalFlowerArea.activeFlowerCount = activeFlowerCount;
        }
    }

    private void Update()
    {
        experimentTime += Time.deltaTime;

        MeasureBirdCrowding();
    }

    public void RegisterNectarCollected(float amount)
    {
        totalNectarCollected += amount;
    }

    public void RegisterFlowerDepleted(Flower flower)
    {
        if (flower == null)
            return;

        if (depletedFlowers.Add(flower))
        {
            flowersDepleted++;
        }
    }

    // Called by FlowerArea when nectar is restored in a flower.
    // Removing it from the set lets the flower be counted again if it is depleted later.
    public void RegisterFlowerRestored(Flower flower)
    {
        if (flower == null)
            return;

        depletedFlowers.Remove(flower);
        flowersRestored++;
    }

    private void MeasureBirdCrowding()
    {
        HummingBirdAgent[] birds =
            FindObjectsByType<HummingBirdAgent>(
                FindObjectsSortMode.None
            );

        HashSet<string> currentCrowdingPairs =
            new HashSet<string>();

        for (int i = 0; i < birds.Length; i++)
        {
            for (int j = i + 1; j < birds.Length; j++)
            {
                float distance = Vector3.Distance(
                    birds[i].transform.position,
                    birds[j].transform.position
                );

                if (distance <= crowdingDistance)
                {
                    string pairKey =
                        GetPairKey(birds[i], birds[j]);

                    currentCrowdingPairs.Add(pairKey);

                    totalCrowdingPairTime += Time.deltaTime;

                    if (!activeCrowdingPairs.Contains(pairKey))
                    {
                        crowdingEvents++;
                    }
                }
            }
        }

        if (currentCrowdingPairs.Count >
            maxSimultaneousCrowdedPairs)
        {
            maxSimultaneousCrowdedPairs =
                currentCrowdingPairs.Count;
        }

        activeCrowdingPairs = currentCrowdingPairs;
    }

    private string GetPairKey(
    HummingBirdAgent birdA,
    HummingBirdAgent birdB)
{
    var idA = birdA.GetEntityId();
    var idB = birdB.GetEntityId();

    if (idA < idB)
    {
        return idA + "_" + idB;
    }

    return idB + "_" + idA;
}

    public float GetExperimentTime()
    {
        return experimentTime;
    }

    public float GetCrowdingRatio()
    {
        int birdCount = numberOfBirds;

        int possiblePairs = birdCount * (birdCount - 1) / 2;

        if (possiblePairs <= 0 || experimentTime <= 0f)
            return 0f;

        return totalCrowdingPairTime /
               (possiblePairs * experimentTime);
    }

    private void SaveResultsToCSV()
    {
        string filePath = Path.Combine(
            Application.persistentDataPath,
            "resource_experiment_results.csv"
        );

        bool fileExists = File.Exists(filePath);

        using (StreamWriter writer = new StreamWriter(filePath, true))
        {
            if (!fileExists)
            {
                writer.WriteLine(
                    "ExperimentID,Birds,ActiveFlowers,NectarCollected," +
                    "FlowersDepleted,DurationSeconds," +
                    "CrowdingDistance,CrowdingEvents," +
                    "CrowdingPairTime,CrowdingRatio," +
                    "MaxSimultaneousCrowdedPairs"
                );
            }

            writer.WriteLine(
                experimentID + "," +
numberOfBirds + "," +
activeFlowerCount + "," +
                totalNectarCollected.ToString("F4") + "," +
                flowersDepleted + "," +
                experimentTime.ToString("F2") + "," +
                crowdingDistance.ToString("F2") + "," +
                crowdingEvents + "," +
                totalCrowdingPairTime.ToString("F2") + "," +
                GetCrowdingRatio().ToString("F4") + "," +
                maxSimultaneousCrowdedPairs
            );
        }

        Debug.Log("CSV saved to:");
        Debug.Log(filePath);
    }
    public void PrintResults()
    {
        if (resultsPrinted)
            return;

        resultsPrinted = true;

        Debug.Log("======================================");
        Debug.Log("      RESOURCE EXPERIMENT RESULT      ");
        Debug.Log("======================================");
        Debug.Log("Experiment ID: " + experimentID);
        Debug.Log("Active Flowers: " + activeFlowerCount);

        Debug.Log(
            "Total Nectar Collected: " +
            totalNectarCollected.ToString("F4")
        );

        Debug.Log(
            "Flowers Depleted: " +
            flowersDepleted
        );

        Debug.Log("Flowers Restored: " + flowersRestored);

        Debug.Log(
            "Experiment Duration: " +
            experimentTime.ToString("F2") +
            " seconds"
        );

        Debug.Log(
            "Crowding Distance: " +
            crowdingDistance.ToString("F2") +
            " units"
        );

        Debug.Log(
            "Crowding Events: " +
            crowdingEvents
        );

        Debug.Log(
    "Total Crowding Pair-Time: " +
    totalCrowdingPairTime.ToString("F2") +
    " pair-seconds"
);
        Debug.Log(
    "Crowding Ratio: " +
    (GetCrowdingRatio() * 100f).ToString("F2") +
    "%"
);

        Debug.Log(
            "Max Simultaneous Crowded Pairs: " +
            maxSimultaneousCrowdedPairs
        );

        Debug.Log("======================================");

        SaveResultsToCSV();

    }

    private void OnApplicationQuit()
    {
        PrintResults();
    }

    private void OnDisable()
    {
        PrintResults();
    }
}