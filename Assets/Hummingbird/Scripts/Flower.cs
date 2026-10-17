using UnityEngine;
using System;

public class Flower : MonoBehaviour
{
    [Tooltip("The color when the flower is pollinated")]
    public Color pollinatedColor = new Color(1f, 0.5f, 0.5f);

    [Tooltip("The color when the flower is not pollinated")]
    public Color notPollinatedColor = new Color(0.5f, 1f, 0.5f);

    // The trigger collider representing the nectar area of the flower
    [HideInInspector]
    public Collider nectarCollider;

    // The solid collider representing the flower petals
    private Collider flowerCollider;

    // The flower material
    private Material flowerMaterial;

    // Raised once when this flower's nectar runs out (FlowerArea listens to this)
    public event Action<Flower> OnDepleted;

    // The flower's up vector
    public Vector3 FlowerUpVector
    {
        get { return nectarCollider.transform.up; }
    }

    // The center position of the nectar collider
    public Vector3 NectarCenterPosition
    {
        get { return nectarCollider.transform.position; }
    }

    // The amount of nectar remaining in the flower
    public float NectorAmount { get; private set; }

    // True when the flower has any nectar left
    public bool HasNector
    {
        get { return NectorAmount > 0f; }
    }

    // Attempts to remove nectar from flower, returns the amount actually removed
    public float Feed(float amount)
    {
        float nectorTaken = Mathf.Clamp(amount, 0f, NectorAmount);
        NectorAmount -= nectorTaken;

        if (NectorAmount <= 0f)
        {
            NectorAmount = 0f;
            flowerCollider.gameObject.SetActive(false);
            nectarCollider.gameObject.SetActive(false);

            flowerMaterial.SetColor("_BaseColor", pollinatedColor);

            // Tell the area so it can restore nectar in another flower later
            OnDepleted?.Invoke(this);
        }
        return nectorTaken;
    }

    // Resets the flower to its initial state
    public void ResetFlower()
    {
        NectorAmount = 1f;
        flowerCollider.gameObject.SetActive(true);
        nectarCollider.gameObject.SetActive(true);
        flowerMaterial.SetColor("_BaseColor", notPollinatedColor);
    }

    public void SetAvailable(bool available)
    {
        if (available)
        {
            NectorAmount = 1f;
            flowerMaterial.SetColor("_BaseColor", notPollinatedColor);
        }
        else
        {
            NectorAmount = 0f;
        }

        flowerCollider.gameObject.SetActive(available);
        nectarCollider.gameObject.SetActive(available);
    }

    private void Awake()
    {
        MeshRenderer flowerMeshRenderer = GetComponentInChildren<MeshRenderer>();
        flowerMaterial = flowerMeshRenderer.material;

        flowerCollider = transform.Find("FlowerCollider").GetComponent<Collider>();
        nectarCollider = transform.Find("FlowerNectarCollider").GetComponent<Collider>();
    }
}
