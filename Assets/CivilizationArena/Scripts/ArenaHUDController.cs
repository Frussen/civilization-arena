using System.Globalization;
using TMPro;
using UnityEngine;

public sealed class ArenaHUDController : MonoBehaviour
{
    [SerializeField] private AgentTreasury agentATreasury;
    [SerializeField] private AgentResourceStockpile agentAResourceStockpile;
    [SerializeField] private TMP_Text hudText;
    [SerializeField] private AgentTreasury agentBTreasury;
    [SerializeField] private AgentResourceStockpile agentBResourceStockpile;
    [SerializeField] private TMP_Text agentBHudText;

    private void Update()
    {
        UpdateDisplay(
            "<color=#E74C3C><b>Agent A</b></color>",
            agentATreasury,
            agentAResourceStockpile,
            hudText);
        UpdateDisplay(
            "<color=#1F6FEB><b>Agent B</b></color>",
            agentBTreasury,
            agentBResourceStockpile,
            agentBHudText);
    }

    private static void UpdateDisplay(
        string agentLabel,
        AgentTreasury treasury,
        AgentResourceStockpile stockpile,
        TMP_Text text)
    {
        if (treasury == null || stockpile == null || text == null)
        {
            return;
        }

        text.text =
            $"{agentLabel}\n" +
            $"Gold: {treasury.CurrentGold.ToString("0", CultureInfo.InvariantCulture)}\n" +
            $"Stone: {stockpile.Stone.ToString("0", CultureInfo.InvariantCulture)}\n" +
            $"Wood: {stockpile.Wood.ToString("0", CultureInfo.InvariantCulture)}";
    }
}
