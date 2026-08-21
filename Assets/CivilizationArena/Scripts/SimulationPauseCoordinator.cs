using System.Collections.Generic;
using UnityEngine;

public readonly struct SimulationPauseLease
{
    internal int Id { get; }
    internal int Generation { get; }

    internal SimulationPauseLease(int id, int generation)
    {
        Id = id;
        Generation = generation;
    }

    public bool IsValid => Id > 0;
}

public static class SimulationPauseCoordinator
{
    private static readonly HashSet<int> ActiveLeaseIds =
        new HashSet<int>();

    private static int generation;
    private static int nextLeaseId;
    private static float resumeTimeScale = 1f;

    public static int ActiveLeaseCount => ActiveLeaseIds.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        ActiveLeaseIds.Clear();
        generation++;
        nextLeaseId = 0;
        resumeTimeScale = 1f;

        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
        }
    }

    public static SimulationPauseLease Acquire()
    {
        if (ActiveLeaseIds.Count == 0)
        {
            resumeTimeScale = Time.timeScale;
        }

        int leaseId = GetNextLeaseId();
        ActiveLeaseIds.Add(leaseId);
        Time.timeScale = 0f;
        return new SimulationPauseLease(leaseId, generation);
    }

    public static bool Release(SimulationPauseLease lease)
    {
        if (!lease.IsValid ||
            lease.Generation != generation ||
            !ActiveLeaseIds.Remove(lease.Id))
        {
            return false;
        }

        if (ActiveLeaseIds.Count > 0)
        {
            Time.timeScale = 0f;
            return true;
        }

        Time.timeScale = resumeTimeScale;
        return true;
    }

    private static int GetNextLeaseId()
    {
        do
        {
            nextLeaseId = nextLeaseId == int.MaxValue
                ? 1
                : nextLeaseId + 1;
        }
        while (ActiveLeaseIds.Contains(nextLeaseId));

        return nextLeaseId;
    }
}
