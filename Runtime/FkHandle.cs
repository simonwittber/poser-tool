using System;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
[ExecuteAlways]
public class FkHandle : MonoBehaviour
{
    public bool enableLookAt;
    
    public AnimationClip clip;
    public Vector3 lookAtPosition;

    public Transform[] lookAtBones;
    public Vector3 boneForward = Vector3.up;

    public Transform[] bones;

    [SerializeReference]
    public List<RotationList> poses = new List<RotationList>();

    public int poseIdCounter = 0;
    
    void Reset()
    {
        bones = GetComponentsInChildren<Transform>();
        SavePose(-1);
    }

    public RotationList SavePose(int parentId)
    {
        var pose = new RotationList() { name = $"Pose {poses.Count}", id = poseIdCounter++, parentId = parentId};
        pose.rotations = new Quaternion[bones.Length];
        for (var i = 0; i < bones.Length; i++)
        {
            pose.rotations[i] = bones[i].localRotation;
        }
        poses.Add(pose);
        return pose;
    }
    
    public void LoadPose(int id)
    {
        var pose = poses.Find(a => a.id == id);
        for (var i = 0; i < bones.Length; i++)
        {
            bones[i].localRotation = pose.rotations[i];
        }
    }

    public void Update()
    {
        if (enableLookAt)
        {
            if (lookAtBones == null || lookAtBones.Length == 0)
                return;
            var finalLookAtRotation = Quaternion.LookRotation(lookAtPosition - transform.position);
            var intialRotation = transform.rotation;
            var step = 1f / lookAtBones.Length;
            for (var i = 0; i < lookAtBones.Length; i++)
            {
                var t = i * step;
                var rotation = Quaternion.Lerp(intialRotation, finalLookAtRotation, t);
                lookAtBones[i].rotation = rotation * Quaternion.Euler(boneForward);
            }
            
            lookAtBones[^1].rotation = finalLookAtRotation * Quaternion.Euler(boneForward);
        }
    }

    [Serializable]
    public class RotationList
    {
        public int id;
        public int parentId = -1;
        public string name;
        public Quaternion[] rotations;
    }
    
}