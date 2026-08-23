using System;
using System.Collections.Generic;
using UnityEngine;

public enum ArenaManualDecisionStatus
{
    Idle,
    WaitingForSubmission,
    Submitted,
    Completed,
    Aborted
}

public delegate bool ArenaManualSubmissionHandler(
    int roundId,
    ArenaSide side,
    ArenaAction action,
    out string error);

public sealed class ArenaManualOfferDraftRow
{
    public string CitizenId { get; }
    public string WorkplaceId { get; }
    public int Wage { get; }

    internal ArenaManualOfferDraftRow(
        string citizenId,
        string workplaceId,
        int wage)
    {
        CitizenId = citizenId;
        WorkplaceId = workplaceId;
        Wage = wage;
    }
}

public sealed class ArenaManualDecisionController : MonoBehaviour
{
    private ArenaManualDecisionStatus status =
        ArenaManualDecisionStatus.Idle;
    private int currentRoundId;
    private ArenaSide activeSide;
    private string capturedObservation;
    private string validationError = string.Empty;
    private string draftStrategyNote = string.Empty;
    private ArenaAction submittedAction;
    private ArenaManualSubmissionHandler submissionHandler;

    private readonly Dictionary<string, int> draftRowIndexByCitizen =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly HashSet<string> allowedWorkplaceIdSet =
        new HashSet<string>(StringComparer.Ordinal);

    private IReadOnlyList<ArenaManualOfferDraftRow> draftRows =
        Array.AsReadOnly(Array.Empty<ArenaManualOfferDraftRow>());
    private ArenaManualOfferDraftRow[] mutableDraftRows =
        Array.Empty<ArenaManualOfferDraftRow>();
    private IReadOnlyList<string> allowedWorkplaceIds =
        Array.AsReadOnly(Array.Empty<string>());

    public ArenaManualDecisionStatus Status => status;
    public int CurrentRoundId => currentRoundId;
    public ArenaSide ActiveSide => activeSide;
    public string CapturedObservation => capturedObservation;
    public string ValidationError => validationError;
    public IReadOnlyList<ArenaManualOfferDraftRow> DraftRows => draftRows;
    public IReadOnlyList<string> AllowedWorkplaceIds =>
        allowedWorkplaceIds;
    public string DraftStrategyNote => draftStrategyNote;
    public ArenaAction SubmittedAction => submittedAction;

    internal bool TryArmForRound(
        int roundId,
        ArenaSide side,
        string observation,
        IReadOnlyCollection<string> allowedCitizenIds,
        IReadOnlyCollection<string> roundAllowedWorkplaceIds,
        ArenaManualSubmissionHandler onSubmit,
        out string error)
    {
        if (status != ArenaManualDecisionStatus.Idle &&
            status != ArenaManualDecisionStatus.Completed &&
            status != ArenaManualDecisionStatus.Aborted)
        {
            return RejectArm(
                "Cannot arm over an active Manual Arena decision.",
                out error);
        }

        if (roundId <= 0)
        {
            return RejectArm(
                "Manual Arena round ID must be greater than zero.",
                out error);
        }

        if (side != ArenaSide.A && side != ArenaSide.B)
        {
            return RejectArm(
                "Manual Arena side must be A or B.",
                out error);
        }

        if (observation == null)
        {
            return RejectArm(
                "Manual Arena observation is required.",
                out error);
        }

        if (!TryCopyIds(
                allowedCitizenIds,
                "citizen",
                out string[] citizenIds,
                out error) ||
            !TryCopyIds(
                roundAllowedWorkplaceIds,
                "workplace",
                out string[] workplaceIds,
                out error))
        {
            validationError = error;
            return false;
        }

        if (onSubmit == null)
        {
            return RejectArm(
                "Manual Arena submission callback is required.",
                out error);
        }

        Array.Sort(citizenIds, StringComparer.Ordinal);
        Array.Sort(workplaceIds, StringComparer.Ordinal);

        ArenaManualOfferDraftRow[] rows =
            new ArenaManualOfferDraftRow[citizenIds.Length];
        string initialWorkplaceId = workplaceIds[0];

        draftRowIndexByCitizen.Clear();
        allowedWorkplaceIdSet.Clear();

        for (int i = 0; i < workplaceIds.Length; i++)
        {
            allowedWorkplaceIdSet.Add(workplaceIds[i]);
        }

        for (int i = 0; i < citizenIds.Length; i++)
        {
            ArenaManualOfferDraftRow row =
                new ArenaManualOfferDraftRow(
                    citizenIds[i],
                    initialWorkplaceId,
                    0);
            rows[i] = row;
            draftRowIndexByCitizen.Add(row.CitizenId, i);
        }

        currentRoundId = roundId;
        activeSide = side;
        capturedObservation = observation;
        mutableDraftRows = rows;
        draftRows = Array.AsReadOnly(rows);
        allowedWorkplaceIds = Array.AsReadOnly(workplaceIds);
        draftStrategyNote = string.Empty;
        submittedAction = null;
        submissionHandler = onSubmit;
        validationError = string.Empty;
        status = ArenaManualDecisionStatus.WaitingForSubmission;

        error = null;
        return true;
    }

    public bool TrySetWorkplace(
        string citizenId,
        string workplaceId)
    {
        if (!CanEditDraft())
        {
            return false;
        }

        if (!draftRowIndexByCitizen.TryGetValue(
                citizenId ?? string.Empty,
                out int rowIndex))
        {
            return Reject($"Unknown citizenId: {citizenId ?? "null"}.");
        }

        if (string.IsNullOrWhiteSpace(workplaceId) ||
            !allowedWorkplaceIdSet.Contains(workplaceId))
        {
            return Reject(
                $"Unknown workplaceId: {workplaceId ?? "null"}.");
        }

        ArenaManualOfferDraftRow row = mutableDraftRows[rowIndex];
        mutableDraftRows[rowIndex] = new ArenaManualOfferDraftRow(
            row.CitizenId,
            workplaceId,
            row.Wage);
        validationError = string.Empty;
        return true;
    }

    public bool TrySetWage(string citizenId, int wage)
    {
        if (!CanEditDraft())
        {
            return false;
        }

        if (!draftRowIndexByCitizen.TryGetValue(
                citizenId ?? string.Empty,
                out int rowIndex))
        {
            return Reject($"Unknown citizenId: {citizenId ?? "null"}.");
        }

        if (wage < 0)
        {
            return Reject("Manual offer wage cannot be negative.");
        }

        ArenaManualOfferDraftRow row = mutableDraftRows[rowIndex];
        mutableDraftRows[rowIndex] = new ArenaManualOfferDraftRow(
            row.CitizenId,
            row.WorkplaceId,
            wage);
        validationError = string.Empty;
        return true;
    }

    public bool SetStrategyNote(string strategyNote)
    {
        if (!CanEditDraft())
        {
            return false;
        }

        if (strategyNote == null)
        {
            return Reject("Manual strategy note cannot be null.");
        }

        draftStrategyNote = strategyNote;
        validationError = string.Empty;
        return true;
    }

    public bool TrySubmit(out string error)
    {
        if (status != ArenaManualDecisionStatus.WaitingForSubmission)
        {
            error = "Manual Arena decision is not waiting for submission.";
            validationError = error;
            return false;
        }

        List<ArenaEmploymentOffer> offers =
            new List<ArenaEmploymentOffer>(draftRows.Count);

        for (int i = 0; i < draftRows.Count; i++)
        {
            ArenaManualOfferDraftRow row = draftRows[i];

            if (row.Wage < 0)
            {
                error =
                    $"Manual offer wage cannot be negative for " +
                    $"{row.CitizenId}.";
                validationError = error;
                return false;
            }

            if (row.Wage == 0)
            {
                continue;
            }

            if (!allowedWorkplaceIdSet.Contains(row.WorkplaceId))
            {
                error =
                    $"Unknown workplaceId for {row.CitizenId}: " +
                    $"{row.WorkplaceId ?? "null"}.";
                validationError = error;
                return false;
            }

            offers.Add(new ArenaEmploymentOffer(
                row.CitizenId,
                row.WorkplaceId,
                row.Wage));
        }

        if (!ArenaActionFactory.TryCreate(
                offers,
                draftStrategyNote,
                out ArenaAction action,
                out error))
        {
            validationError = error;
            return false;
        }

        if (submissionHandler == null)
        {
            error = "Manual Arena submission callback is unavailable.";
            validationError = error;
            return false;
        }

        int submissionRoundId = currentRoundId;
        ArenaManualSubmissionHandler handler = submissionHandler;
        submittedAction = action;
        submissionHandler = null;
        status = ArenaManualDecisionStatus.Submitted;

        bool accepted;

        try
        {
            accepted = handler(
                submissionRoundId,
                activeSide,
                action,
                out string submissionError);

            if (!accepted)
            {
                error = string.IsNullOrWhiteSpace(submissionError)
                    ? "Manual Arena submission was rejected."
                    : submissionError;

                RestoreRejectedSubmissionIfStillProvisional(
                    submissionRoundId,
                    action,
                    handler);

                if (currentRoundId == submissionRoundId)
                {
                    validationError = error;
                }

                return false;
            }
        }
        catch (Exception exception)
        {
            error =
                $"Manual Arena submission failed: {exception.Message}";
            RestoreRejectedSubmissionIfStillProvisional(
                submissionRoundId,
                action,
                handler);

            if (currentRoundId == submissionRoundId)
            {
                validationError = error;
            }

            return false;
        }

        if (currentRoundId == submissionRoundId &&
            status == ArenaManualDecisionStatus.Submitted &&
            ReferenceEquals(submittedAction, action))
        {
            validationError = string.Empty;
        }

        error = null;
        return true;
    }

    internal bool CompleteRound(int roundId, out string error)
    {
        if (roundId != currentRoundId)
        {
            error = "Cannot complete a stale Manual Arena round.";
            validationError = error;
            return false;
        }

        if (status != ArenaManualDecisionStatus.Submitted)
        {
            error = "Only a submitted Manual Arena decision can complete.";
            validationError = error;
            return false;
        }

        submissionHandler = null;
        status = ArenaManualDecisionStatus.Completed;
        validationError = string.Empty;
        error = null;
        return true;
    }

    internal bool AbortRound(int roundId, out string error)
    {
        if (roundId != currentRoundId)
        {
            error = "Cannot abort a stale Manual Arena round.";
            validationError = error;
            return false;
        }

        if (status == ArenaManualDecisionStatus.Completed ||
            status == ArenaManualDecisionStatus.Aborted ||
            status == ArenaManualDecisionStatus.Idle)
        {
            error = "Manual Arena decision is not active.";
            validationError = error;
            return false;
        }

        submissionHandler = null;
        status = ArenaManualDecisionStatus.Aborted;
        validationError = string.Empty;
        error = null;
        return true;
    }

    private bool CanEditDraft()
    {
        if (status == ArenaManualDecisionStatus.WaitingForSubmission)
        {
            return true;
        }

        return Reject("Manual Arena draft is not editable.");
    }

    private bool Reject(string error)
    {
        validationError = error;
        return false;
    }

    private void RestoreRejectedSubmissionIfStillProvisional(
        int roundId,
        ArenaAction action,
        ArenaManualSubmissionHandler handler)
    {
        if (currentRoundId != roundId ||
            status != ArenaManualDecisionStatus.Submitted ||
            !ReferenceEquals(submittedAction, action) ||
            submissionHandler != null)
        {
            return;
        }

        submittedAction = null;
        submissionHandler = handler;
        status = ArenaManualDecisionStatus.WaitingForSubmission;
    }

    private bool RejectArm(string message, out string error)
    {
        validationError = message;
        error = message;
        return false;
    }

    private static bool TryCopyIds(
        IReadOnlyCollection<string> ids,
        string idKind,
        out string[] copiedIds,
        out string error)
    {
        copiedIds = Array.Empty<string>();

        if (ids == null)
        {
            error = $"Allowed Manual Arena {idKind} IDs are required.";
            return false;
        }

        if (ids.Count == 0)
        {
            error =
                $"At least one Manual Arena {idKind} ID is required.";
            return false;
        }

        copiedIds = new string[ids.Count];
        HashSet<string> uniqueIds = new HashSet<string>(
            StringComparer.Ordinal);
        int index = 0;

        foreach (string id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                error =
                    $"Allowed Manual Arena {idKind} IDs cannot be blank.";
                copiedIds = Array.Empty<string>();
                return false;
            }

            if (!uniqueIds.Add(id))
            {
                error =
                    $"Duplicate Manual Arena {idKind} ID: {id}.";
                copiedIds = Array.Empty<string>();
                return false;
            }

            copiedIds[index++] = id;
        }

        error = null;
        return true;
    }
}
