using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ItemPickup : MonoBehaviour
{
    [Header("Item")]
    public string itemName = "Schlüssel";
    public bool destroyOnPickup = true;
    public bool pickupOnce = true;

    bool alreadyPickedUp;

    void OnTriggerEnter(Collider other)
    {
        if (alreadyPickedUp && pickupOnce)
            return;

        player playerScript = other.GetComponent<player>();
        if (playerScript == null)
            playerScript = other.GetComponentInParent<player>();

        if (playerScript == null)
            return;

        bool added = playerScript.AddItem(itemName);
        if (!added)
            return;

        alreadyPickedUp = true;
        Debug.Log("Item aufgenommen: " + itemName);

        if (destroyOnPickup)
            Destroy(gameObject);
    }
}
