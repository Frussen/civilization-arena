using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(SimulationExecutionOrder.TreasuryEconomy)]
public class AgentTreasury : MonoBehaviour
{
    [SerializeField] private WorldClock clock;
    [SerializeField] private float initialGold = 100f;
    [SerializeField] private float goldIncomePerHour = 40f;
    [SerializeField] private float payrollCoverageHours = 10f;

    [SerializeField] private float currentGold;
    [SerializeField] private float currentPayrollPerHour;

    private readonly List<CitizenEmployment> employees =
        new List<CitizenEmployment>();
    private readonly List<CitizenEmployment> employeesForTick =
        new List<CitizenEmployment>();

    public float CurrentGold => currentGold;
    public float GoldIncomePerHour => goldIncomePerHour;
    public float CurrentPayrollPerHour => currentPayrollPerHour;
    public float PayrollCoverageHours => Mathf.Max(0f, payrollCoverageHours);

    private void Awake()
    {
        currentGold = Mathf.Max(0f, initialGold);
        currentPayrollPerHour = 0f;
    }

    private void Update()
    {
        int simulatedMinutes = clock.MinutesAdvancedThisFrame;
        if (simulatedMinutes <= 0)
        {
            return;
        }

        float income = goldIncomePerHour * simulatedMinutes / 60f;
        currentGold = Mathf.Max(0f, currentGold + income);

        if (!TryBuildOrderedEmployeeSnapshot())
        {
            return;
        }

        for (int i = 0; i < employeesForTick.Count; i++)
        {
            CitizenEmployment employee = employeesForTick[i];
            if (employee != null &&
                employee.isActiveAndEnabled &&
                employee.CurrentEmployer == this)
            {
                employee.ProcessWage(this, simulatedMinutes);
            }
        }

        BeginWorkerTick();
        ProcessWork(simulatedMinutes, processWonderWork: false);
        ProcessWork(simulatedMinutes, processWonderWork: true);
    }

    public bool TrySpend(float amount)
    {
        if (amount < 0f || amount > currentGold)
        {
            return false;
        }

        currentGold -= amount;
        return true;
    }

    public bool HasPayrollCoverage(float projectedPayrollPerHour)
    {
        if (projectedPayrollPerHour < 0f)
        {
            return false;
        }

        float requiredGold = projectedPayrollPerHour * PayrollCoverageHours;
        return currentGold >= requiredGold;
    }

    private void AddPayrollCommitment(int wagePerHour)
    {
        if (wagePerHour > 0)
        {
            currentPayrollPerHour += wagePerHour;
        }
    }

    private void RemovePayrollCommitment(int wagePerHour)
    {
        if (wagePerHour > 0)
        {
            currentPayrollPerHour = Mathf.Max(
                0f,
                currentPayrollPerHour - wagePerHour);
        }
    }

    internal void RegisterEmployee(
        CitizenEmployment employee,
        int wagePerHour)
    {
        if (employee == null || wagePerHour <= 0)
        {
            return;
        }

        if (employees.Contains(employee))
        {
            return;
        }

        employees.Add(employee);
        AddPayrollCommitment(wagePerHour);
    }

    internal void UnregisterEmployee(
        CitizenEmployment employee,
        int wagePerHour)
    {
        if (employee == null || !employees.Remove(employee))
        {
            return;
        }

        RemovePayrollCommitment(wagePerHour);
    }

    private bool TryBuildOrderedEmployeeSnapshot()
    {
        employeesForTick.Clear();
        employeesForTick.AddRange(employees);

        for (int i = 0; i < employeesForTick.Count; i++)
        {
            if (employeesForTick[i] == null)
            {
                Debug.LogError(
                    $"{name} contains a destroyed employee whose payroll " +
                    "commitment could not be reconciled.",
                    this);
                return false;
            }
        }

        employeesForTick.Sort(CompareEmployeesByCitizenId);

        for (int i = 0; i < employeesForTick.Count; i++)
        {
            CitizenEmployment employee = employeesForTick[i];
            string citizenId = employee.gameObject.name;

            if (string.IsNullOrWhiteSpace(citizenId))
            {
                Debug.LogError(
                    $"{name} cannot process an employee with a blank citizen ID.",
                    this);
                return false;
            }

            if (i > 0 && string.Equals(
                    employeesForTick[i - 1].gameObject.name,
                    citizenId,
                    StringComparison.Ordinal))
            {
                Debug.LogError(
                    $"{name} cannot deterministically process duplicate " +
                    $"citizen ID '{citizenId}'.",
                    this);
                return false;
            }
        }

        return true;
    }

    private void ProcessWork(int simulatedMinutes, bool processWonderWork)
    {
        for (int i = 0; i < employeesForTick.Count; i++)
        {
            CitizenEmployment employee = employeesForTick[i];
            if (employee == null || employee.CurrentEmployer != this)
            {
                continue;
            }

            CitizenWorker worker = employee.GetComponent<CitizenWorker>();
            if (worker == null || !worker.isActiveAndEnabled)
            {
                continue;
            }

            if (processWonderWork)
            {
                worker.ProcessWonderWork(this, simulatedMinutes);
            }
            else
            {
                worker.ProcessResourceWork(this, simulatedMinutes);
            }
        }
    }

    private void BeginWorkerTick()
    {
        for (int i = 0; i < employeesForTick.Count; i++)
        {
            CitizenEmployment employee = employeesForTick[i];
            CitizenWorker worker = employee != null
                ? employee.GetComponent<CitizenWorker>()
                : null;
            worker?.BeginWorkTick();
        }
    }

    private static int CompareEmployeesByCitizenId(
        CitizenEmployment left,
        CitizenEmployment right)
    {
        return StringComparer.Ordinal.Compare(
            left.gameObject.name,
            right.gameObject.name);
    }
}
