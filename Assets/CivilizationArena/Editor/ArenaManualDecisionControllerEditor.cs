using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ArenaManualDecisionController))]
public sealed class ArenaManualDecisionControllerEditor : Editor
{
    private Vector2 observationScroll;

    public override void OnInspectorGUI()
    {
        ArenaManualDecisionController controller =
            (ArenaManualDecisionController)target;

        if (controller == null)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Manual Arena decisions become available in Play Mode when " +
                "this controller is armed for a round.",
                MessageType.Info);
            return;
        }

        DrawRoundState(controller);

        bool canEdit =
            controller.isActiveAndEnabled &&
            controller.Status ==
                ArenaManualDecisionStatus.WaitingForSubmission;

        if (!controller.isActiveAndEnabled)
        {
            EditorGUILayout.HelpBox(
                "This Manual decision controller is disabled.",
                MessageType.Warning);
        }

        DrawCapturedObservation(controller.CapturedObservation);
        DrawStrategyNote(controller, canEdit);
        DrawOfferRows(controller, canEdit);

        if (!string.IsNullOrWhiteSpace(controller.ValidationError))
        {
            EditorGUILayout.HelpBox(
                controller.ValidationError,
                MessageType.Error);
        }

        if (canEdit)
        {
            EditorGUILayout.Space();

            if (GUILayout.Button("Submit Manual Arena Action"))
            {
                controller.TrySubmit(out _);
                Repaint();
            }
        }
    }

    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    private static void DrawRoundState(
        ArenaManualDecisionController controller)
    {
        EditorGUILayout.LabelField("Manual Arena Decision", EditorStyles.boldLabel);

        bool hasRound =
            controller.Status != ArenaManualDecisionStatus.Idle;
        EditorGUILayout.LabelField(
            "Active Side",
            hasRound ? controller.ActiveSide.ToString() : "None");
        EditorGUILayout.LabelField(
            "Round ID",
            hasRound ? controller.CurrentRoundId.ToString() : "None");
        EditorGUILayout.LabelField("Status", controller.Status.ToString());

        MessageType messageType;
        string message;

        switch (controller.Status)
        {
            case ArenaManualDecisionStatus.WaitingForSubmission:
                messageType = MessageType.Info;
                message = "Waiting for a Manual decision.";
                break;
            case ArenaManualDecisionStatus.Submitted:
                messageType = MessageType.Info;
                message = "Decision submitted. The draft is read-only.";
                break;
            case ArenaManualDecisionStatus.Completed:
                messageType = MessageType.Info;
                message = "This Manual Arena decision is complete.";
                break;
            case ArenaManualDecisionStatus.Aborted:
                messageType = MessageType.Warning;
                message = "This Manual Arena decision was aborted.";
                break;
            default:
                messageType = MessageType.Info;
                message = "No Manual Arena round is currently waiting.";
                break;
        }

        EditorGUILayout.HelpBox(message, messageType);
    }

    private void DrawCapturedObservation(string observation)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Captured Observation", EditorStyles.boldLabel);

        string displayedObservation = observation ?? string.Empty;

        using (new EditorGUI.DisabledScope(true))
        {
            observationScroll = EditorGUILayout.BeginScrollView(
                observationScroll,
                GUILayout.MinHeight(160f),
                GUILayout.MaxHeight(260f));
            EditorGUILayout.TextArea(
                displayedObservation,
                GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }

    private static void DrawStrategyNote(
        ArenaManualDecisionController controller,
        bool canEdit)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Strategy Note", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!canEdit))
        {
            EditorGUI.BeginChangeCheck();
            string strategyNote = EditorGUILayout.TextArea(
                controller.DraftStrategyNote ?? string.Empty,
                GUILayout.MinHeight(48f));

            if (EditorGUI.EndChangeCheck() && canEdit)
            {
                controller.SetStrategyNote(strategyNote);
            }
        }
    }

    private static void DrawOfferRows(
        ArenaManualDecisionController controller,
        bool canEdit)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Employment Offers", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Wage 0 means no offer.",
            MessageType.None);

        IReadOnlyList<ArenaManualOfferDraftRow> rows =
            controller.DraftRows;
        IReadOnlyList<string> allowedWorkplaceIds =
            controller.AllowedWorkplaceIds;

        if (rows == null || rows.Count == 0)
        {
            EditorGUILayout.LabelField("No draft rows are available.");
            return;
        }

        string[] workplaceOptions = CopyWorkplaceOptions(
            allowedWorkplaceIds);

        if (workplaceOptions.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "No allowed workplaces are available.",
                MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(!canEdit))
        {
            for (int i = 0; i < rows.Count; i++)
            {
                DrawOfferRow(
                    controller,
                    rows[i],
                    workplaceOptions,
                    canEdit);
            }
        }
    }

    private static void DrawOfferRow(
        ArenaManualDecisionController controller,
        ArenaManualOfferDraftRow row,
        string[] workplaceOptions,
        bool canEdit)
    {
        if (row == null)
        {
            EditorGUILayout.LabelField("Invalid draft row.");
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            row.CitizenId ?? string.Empty,
            GUILayout.MinWidth(100f));

        if (workplaceOptions.Length > 0)
        {
            int workplaceIndex = FindOrdinalIndex(
                workplaceOptions,
                row.WorkplaceId);
            int displayedIndex = Math.Max(0, workplaceIndex);
            int selectedIndex = EditorGUILayout.Popup(
                displayedIndex,
                workplaceOptions,
                GUILayout.MinWidth(120f));

            if (canEdit && selectedIndex != displayedIndex)
            {
                controller.TrySetWorkplace(
                    row.CitizenId,
                    workplaceOptions[selectedIndex]);
            }
        }
        else
        {
            EditorGUILayout.LabelField("No workplace");
        }

        EditorGUILayout.LabelField("@", GUILayout.Width(14f));
        int wage = Math.Max(
            0,
            EditorGUILayout.IntField(row.Wage, GUILayout.Width(64f)));

        if (canEdit && wage != row.Wage)
        {
            controller.TrySetWage(row.CitizenId, wage);
        }

        EditorGUILayout.EndHorizontal();
    }

    private static string[] CopyWorkplaceOptions(
        IReadOnlyList<string> workplaceIds)
    {
        if (workplaceIds == null || workplaceIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        string[] options = new string[workplaceIds.Count];

        for (int i = 0; i < workplaceIds.Count; i++)
        {
            options[i] = workplaceIds[i];
        }

        return options;
    }

    private static int FindOrdinalIndex(
        string[] values,
        string targetValue)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (string.Equals(
                    values[i],
                    targetValue,
                    StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
