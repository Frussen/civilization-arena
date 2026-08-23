public enum CitizenActivity
{
    Unemployed,
    Idle,
    Resting,
    Working,
    TravelingToWork,
    TravelingToRest
}

public static class CitizenActivityClassifier
{
    public static CitizenActivity GetActivity(CitizenEmployment citizen)
    {
        if (citizen == null)
        {
            return CitizenActivity.Idle;
        }

        if (!citizen.IsEmployed)
        {
            return CitizenActivity.Unemployed;
        }

        CitizenRoutine routine = citizen.GetComponent<CitizenRoutine>();
        CitizenWorkAssignment assignment =
            citizen.GetComponent<CitizenWorkAssignment>();

        if (routine == null || assignment == null)
        {
            return CitizenActivity.Idle;
        }

        if (routine.DestinationPurpose == CitizenDestinationPurpose.Work)
        {
            Workplace workplace = assignment.CurrentWorkplace;

            if (workplace == null ||
                !routine.IsCurrentDestination(workplace.transform))
            {
                return CitizenActivity.Idle;
            }

            return workplace.IsWithinWorkArea(citizen.transform.position)
                ? CitizenActivity.Working
                : CitizenActivity.TravelingToWork;
        }

        if (routine.DestinationPurpose == CitizenDestinationPurpose.Rest)
        {
            if (!routine.HasCurrentDestination)
            {
                return CitizenActivity.Idle;
            }

            return routine.HasArrivedAtDestination
                ? CitizenActivity.Resting
                : CitizenActivity.TravelingToRest;
        }

        return CitizenActivity.Idle;
    }
}
