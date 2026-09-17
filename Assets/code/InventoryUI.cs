using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    public player targetPlayer;
    public Text inventoryText;

    void Start()
    {
        if (targetPlayer == null)
            targetPlayer = FindObjectOfType<player>();

        if (inventoryText == null)
            inventoryText = GetComponentInChildren<Text>();

        RefreshInventory();
    }

    void Update()
    {
        RefreshInventory();
    }

    public void RefreshInventory()
    {
        if (targetPlayer == null || inventoryText == null)
            return;

        if (targetPlayer.items.Count == 0)
        {
            inventoryText.text = "Inventar:\nleer";
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Inventar:");

        foreach (string item in targetPlayer.items)
        {
            sb.AppendLine("- " + item);
        }

        inventoryText.text = sb.ToString();
    }
}
