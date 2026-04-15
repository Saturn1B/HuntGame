using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class NonHumanoidRagdollSetupWindow : EditorWindow
{
    [SerializeField] private Transform skeletonRoot;
    [SerializeField] private bool onlySkinnedBones = true;
    [SerializeField] private float defaultMass = 1f;
    [SerializeField] private PhysicsMaterial colliderMaterial;
    [SerializeField] private bool addCapsuleColliders = true;
    [SerializeField] private bool startKinematic = true;

    private Vector2 _scroll;
    private List<Transform> _bones = new List<Transform>();
    private List<bool> _boneEnabled = new List<bool>();

    [MenuItem("Tools/Hunting Game/Non-Humanoid Ragdoll Setup")]
    public static void ShowWindow()
    {
        GetWindow<NonHumanoidRagdollSetupWindow>("Non-Humanoid Ragdoll");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Target Skeleton", EditorStyles.boldLabel);
        skeletonRoot = (Transform)EditorGUILayout.ObjectField("Skeleton Root", skeletonRoot, typeof(Transform), true);

        onlySkinnedBones = EditorGUILayout.Toggle("Only Skinned Bones", onlySkinnedBones);
        defaultMass = EditorGUILayout.FloatField("Default Mass", defaultMass);
        startKinematic = EditorGUILayout.Toggle("Start Kinematic", startKinematic);
        addCapsuleColliders = EditorGUILayout.Toggle("Add Capsule Colliders", addCapsuleColliders);
        colliderMaterial = (PhysicsMaterial)EditorGUILayout.ObjectField("Collider Material", colliderMaterial, typeof(PhysicsMaterial), false);

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Scan Bones"))
            ScanBones();
        GUI.enabled = _bones.Count > 0;
        if (GUILayout.Button("Select All"))
            SetAllBones(true);
        if (GUILayout.Button("Deselect All"))
            SetAllBones(false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        GUI.enabled = _bones.Count > 0;
        EditorGUILayout.LabelField("Bones", EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(200));
        for (int i = 0; i < _bones.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _boneEnabled[i] = EditorGUILayout.Toggle(_boneEnabled[i], GUILayout.Width(20));
            EditorGUILayout.ObjectField(_bones[i], typeof(Transform), true);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        if (GUILayout.Button("Build / Update Ragdoll", GUILayout.Height(30)))
            BuildRagdoll();

        if (GUILayout.Button("Remove Ragdoll Components", GUILayout.Height(20)))
            RemoveRagdoll();

        GUI.enabled = true;
    }

    void ScanBones()
    {
        _bones.Clear();
        _boneEnabled.Clear();

        if (skeletonRoot == null)
        {
            EditorUtility.DisplayDialog("Error", "Assign a Skeleton Root first.", "OK");
            return;
        }

        HashSet<Transform> validBones = null;

        if (onlySkinnedBones)
        {
            validBones = new HashSet<Transform>();
            var renderers = skeletonRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in renderers)
            {
                foreach (var b in r.bones)
                {
                    if (b != null)
                        validBones.Add(b);
                }
            }
        }

        foreach (Transform t in skeletonRoot.GetComponentsInChildren<Transform>(true))
        {
            if (onlySkinnedBones && (validBones == null || !validBones.Contains(t)))
                continue;

            _bones.Add(t);
            _boneEnabled.Add(true);
        }
    }

    void SetAllBones(bool enabled)
    {
        for (int i = 0; i < _boneEnabled.Count; i++)
            _boneEnabled[i] = enabled;
    }

    void BuildRagdoll()
    {
        if (skeletonRoot == null)
        {
            EditorUtility.DisplayDialog("Error", "Assign a Skeleton Root first.", "OK");
            return;
        }

        // Ensure root has a Rigidbody
        var rootRb = skeletonRoot.GetComponent<Rigidbody>();
        if (rootRb == null)
        {
            rootRb = Undo.AddComponent<Rigidbody>(skeletonRoot.gameObject);
            rootRb.mass = defaultMass;
        }
        rootRb.isKinematic = startKinematic;

        var boneToRb = new Dictionary<Transform, Rigidbody>
        {
            { skeletonRoot, rootRb }
        };

        // Create Rigidbodies and colliders
        for (int i = 0; i < _bones.Count; i++)
        {
            var bone = _bones[i];
            if (bone == null || !_boneEnabled[i])
                continue;

            if (!boneToRb.TryGetValue(bone, out var rb))
            {
                rb = bone.GetComponent<Rigidbody>();
                if (rb == null)
                    rb = Undo.AddComponent<Rigidbody>(bone.gameObject);

                rb.mass = defaultMass;
                rb.isKinematic = startKinematic;
                boneToRb[bone] = rb;
            }

            if (addCapsuleColliders)
                EnsureCapsuleCollider(bone, colliderMaterial);
        }

        // Add joints (CharacterJoint) from child to parent
        for (int i = 0; i < _bones.Count; i++)
        {
            var bone = _bones[i];
            if (bone == null || !_boneEnabled[i] || bone == skeletonRoot)
                continue;

            var parent = bone.parent;
            if (parent == null) continue;

            if (!boneToRb.TryGetValue(bone, out var childRb)) continue;
            if (!boneToRb.TryGetValue(parent, out var parentRb)) continue;

            var joint = bone.GetComponent<CharacterJoint>();
            if (joint == null)
                joint = Undo.AddComponent<CharacterJoint>(bone.gameObject);

            joint.connectedBody = parentRb;

            // Simple default limits – tweak as needed
            joint.lowTwistLimit = new SoftJointLimit { limit = -20f };
            joint.highTwistLimit = new SoftJointLimit { limit = 20f };
            joint.swing1Limit = new SoftJointLimit { limit = 30f };
            joint.swing2Limit = new SoftJointLimit { limit = 30f };
        }

        EditorUtility.DisplayDialog("Ragdoll", "Ragdoll setup completed.", "OK");
    }

    void EnsureCapsuleCollider(Transform bone, PhysicsMaterial mat)
    {
        if (bone.GetComponent<Collider>() != null)
            return;

        // If no children, bail or give a tiny collider.
        if (bone.childCount == 0)
            return;

        // Find furthest child
        Transform furthestChild = null;
        float maxDist = 0f;
        foreach (Transform child in bone)
        {
            float d = Vector3.Distance(bone.position, child.position);
            if (d > maxDist)
            {
                maxDist = d;
                furthestChild = child;
            }
        }
        if (furthestChild == null || maxDist <= 0f)
            return;

        var capsule = bone.gameObject.AddComponent<CapsuleCollider>();

        // Direction in world
        Vector3 dirWorld = (furthestChild.position - bone.position).normalized;

        // Choose closest local axis (X/Y/Z) to align capsule to
        Vector3 dirLocal = bone.InverseTransformDirection(dirWorld);
        dirLocal.Normalize();
        float ax = Mathf.Abs(dirLocal.x);
        float ay = Mathf.Abs(dirLocal.y);
        float az = Mathf.Abs(dirLocal.z);

        int axis; // 0 = X, 1 = Y, 2 = Z
        if (ax > ay && ax > az) axis = 0;
        else if (ay > az) axis = 1;
        else axis = 2;
        capsule.direction = axis;

        // Height and center
        float length = maxDist;
        capsule.height = Mathf.Max(0.01f, length);

        float radius = length * 0.25f; // tweak
        capsule.radius = radius;

        // Center halfway between bone and child, expressed in bone local space
        Vector3 midWorld = (bone.position + furthestChild.position) * 0.5f;
        Vector3 midLocal = bone.InverseTransformPoint(midWorld);
        capsule.center = midLocal;

        capsule.material = mat;
    }

    void RemoveRagdoll()
    {
        if (skeletonRoot == null)
        {
            EditorUtility.DisplayDialog("Error", "Assign a Skeleton Root first.", "OK");
            return;
        }

        var rbs = skeletonRoot.GetComponentsInChildren<Rigidbody>(true);
        var joints = skeletonRoot.GetComponentsInChildren<CharacterJoint>(true);
        var colliders = skeletonRoot.GetComponentsInChildren<Collider>(true);

        foreach (var j in joints)
            Undo.DestroyObjectImmediate(j);

        foreach (var rb in rbs)
            Undo.DestroyObjectImmediate(rb);

        foreach (var c in colliders)
            Undo.DestroyObjectImmediate(c);

        EditorUtility.DisplayDialog("Ragdoll", "All ragdoll components removed under root.", "OK");
    }
}