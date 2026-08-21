using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ArenaCitizenTeamVisualController : MonoBehaviour
{
    private static readonly Vector3 ShirtLocalPosition =
        new Vector3(0f, 0.14f, -0.03f);
    private static readonly Vector3 ShirtLocalScale =
        new Vector3(0.45f, 0.5f, 0.29f);

    [SerializeField] private ArenaRoundSnapshotBuilder snapshotBuilder;
    [SerializeField] private Material citizenTeamRed;
    [SerializeField] private Material citizenTeamBlue;

    private readonly List<CitizenShirt> shirts =
        new List<CitizenShirt>();

    private void Start()
    {
        if (snapshotBuilder == null ||
            citizenTeamRed == null ||
            citizenTeamBlue == null)
        {
            Debug.LogError(
                "Arena citizen team visuals are not fully configured.",
                this);
            enabled = false;
            return;
        }

        if (!snapshotBuilder.TryGetConfiguredCitizens(
                out IReadOnlyDictionary<string, CitizenEmployment> citizens,
                out string error))
        {
            Debug.LogError(
                $"Arena citizen team visual setup failed: {error}",
                this);
            enabled = false;
            return;
        }

        List<string> citizenIds = new List<string>(citizens.Keys);
        citizenIds.Sort(StringComparer.Ordinal);

        Mesh cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        for (int i = 0; i < citizenIds.Count; i++)
        {
            CitizenEmployment citizen = citizens[citizenIds[i]];
            if (!TryCreateShirt(citizen, cubeMesh, out CitizenShirt shirt))
            {
                enabled = false;
                return;
            }

            shirts.Add(shirt);
        }

        UpdateShirts();
    }

    private void Update()
    {
        UpdateShirts();
    }

    private void UpdateShirts()
    {
        for (int i = 0; i < shirts.Count; i++)
        {
            CitizenShirt shirt = shirts[i];
            if (shirt.Citizen == null || shirt.GameObject == null)
            {
                continue;
            }

            AgentTreasury employer = shirt.Citizen.CurrentEmployer;
            if (shirt.Initialized && shirt.DisplayedEmployer == employer)
            {
                continue;
            }

            shirt.Initialized = true;
            shirt.DisplayedEmployer = employer;

            if (employer == snapshotBuilder.SideATreasury)
            {
                shirt.Renderer.sharedMaterial = citizenTeamRed;
                shirt.GameObject.SetActive(true);
            }
            else if (employer == snapshotBuilder.SideBTreasury)
            {
                shirt.Renderer.sharedMaterial = citizenTeamBlue;
                shirt.GameObject.SetActive(true);
            }
            else
            {
                shirt.GameObject.SetActive(false);
            }
        }
    }

    private bool TryCreateShirt(
        CitizenEmployment citizen,
        Mesh cubeMesh,
        out CitizenShirt shirt)
    {
        shirt = null;
        Transform spine = FindDescendant(citizen.transform, "spine_02");
        if (spine == null)
        {
            Debug.LogError(
                $"Citizen '{citizen.gameObject.name}' has no spine_02 bone.",
                citizen);
            return false;
        }

        Transform shirtTransform = FindDescendant(
            citizen.transform,
            "TeamShirt");
        GameObject shirtObject;

        if (shirtTransform != null)
        {
            shirtObject = shirtTransform.gameObject;
            shirtTransform.SetParent(spine, false);
        }
        else
        {
            shirtObject = new GameObject("TeamShirt");
            shirtTransform = shirtObject.transform;
            shirtTransform.SetParent(spine, false);
        }

        shirtTransform.localPosition = ShirtLocalPosition;
        shirtTransform.localRotation = Quaternion.identity;
        shirtTransform.localScale = ShirtLocalScale;

        MeshFilter meshFilter = shirtObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = shirtObject.AddComponent<MeshFilter>();
        }

        if (meshFilter.sharedMesh == null)
        {
            meshFilter.sharedMesh = cubeMesh;
        }

        MeshRenderer renderer = shirtObject.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = shirtObject.AddComponent<MeshRenderer>();
        }

        shirt = new CitizenShirt(citizen, shirtObject, renderer);
        return true;
    }

    private static Transform FindDescendant(Transform root, string childName)
    {
        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindDescendant(root.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private sealed class CitizenShirt
    {
        public CitizenEmployment Citizen { get; }
        public GameObject GameObject { get; }
        public MeshRenderer Renderer { get; }
        public AgentTreasury DisplayedEmployer { get; set; }
        public bool Initialized { get; set; }

        public CitizenShirt(
            CitizenEmployment citizen,
            GameObject gameObject,
            MeshRenderer renderer)
        {
            Citizen = citizen;
            GameObject = gameObject;
            Renderer = renderer;
        }
    }
}
