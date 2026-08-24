using System.Globalization;
using TMPro;
using UnityEngine;

public sealed class ArenaHUDController : MonoBehaviour
{
    [SerializeField] private ArenaLlmRoundController roundController;
    [SerializeField] private AgentTreasury agentATreasury;
    [SerializeField] private AgentResourceStockpile agentAResourceStockpile;
    [SerializeField] private WonderConstruction agentAWonder;
    [SerializeField] private TMP_Text hudText;
    [SerializeField] private AgentTreasury agentBTreasury;
    [SerializeField] private AgentResourceStockpile agentBResourceStockpile;
    [SerializeField] private WonderConstruction agentBWonder;
    [SerializeField] private TMP_Text agentBHudText;

    private void Update()
    {
        UpdateDisplay(
            FormatSideLabel(ArenaSide.A, "#E74C3C"),
            agentATreasury,
            agentAResourceStockpile,
            agentAWonder,
            hudText);
        UpdateDisplay(
            FormatSideLabel(ArenaSide.B, "#1F6FEB"),
            agentBTreasury,
            agentBResourceStockpile,
            agentBWonder,
            agentBHudText);
    }

    private string FormatSideLabel(ArenaSide side, string color)
    {
        bool isManual = roundController != null &&
            (side == ArenaSide.A
                ? roundController.SideAControlMode
                : roundController.SideBControlMode) ==
            AgentControlMode.Manual;
        string role = isManual ? "Player" : "Agent";
        return $"<color={color}><b>{role} {side}</b></color>";
    }

    private static void UpdateDisplay(
        string agentLabel,
        AgentTreasury treasury,
        AgentResourceStockpile stockpile,
        WonderConstruction wonder,
        TMP_Text text)
    {
        if (treasury == null || stockpile == null || text == null)
        {
            return;
        }

        text.text =
            $"{agentLabel}\n" +
            $"Gold: {FormatWholeUnits(treasury.CurrentGold)}\n" +
            $"Stone: {FormatWholeUnits(stockpile.Stone)}\n" +
            $"Wood: {FormatWholeUnits(stockpile.Wood)}\n" +
            $"Wonder: {FormatWonderProgress(wonder)}";
    }

    private static string FormatWholeUnits(float value)
    {
        float wholeUnits = value >= 0f
            ? Mathf.Floor(value)
            : Mathf.Ceil(value);
        return wholeUnits.ToString("0", CultureInfo.InvariantCulture);
    }

    private static string FormatWonderProgress(WonderConstruction wonder)
    {
        if (wonder == null)
        {
            return "—";
        }

        if (wonder.Completed)
        {
            return "100.0%";
        }

        float completed = wonder.LaborHoursCompleted;
        float required = wonder.LaborHoursRequired;

        if (required <= 0f ||
            float.IsNaN(required) ||
            float.IsInfinity(required) ||
            float.IsNaN(completed) ||
            float.IsInfinity(completed))
        {
            return "—";
        }

        float percentage = Mathf.Clamp01(completed / required) * 100f;
        return percentage.ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }
}
