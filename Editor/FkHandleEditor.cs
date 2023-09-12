using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;


[Serializable]
//The TreeElement data class is extended to hold extra data, which you can show and edit in the front-end TreeView.
internal class PoseTreeView : TreeView
{
    private readonly FkHandle _handle;
    private Dictionary<int,TreeViewItem> parentMap = new();
    private Dictionary<int, FkHandle.RotationList> poseMap = new();

    public PoseTreeView(TreeViewState state, FkHandle handle) : base(state)
    {
        this._handle = handle;
        Reload();
    }

    protected override TreeViewItem BuildRoot()
    {
        var root = new TreeViewItem {id = 0, depth = -1, displayName = "Root"};
        parentMap.Clear();
        poseMap.Clear();
        foreach(var pose in _handle.poses)
        {
            if(pose == null) continue;
            var poseItem = new TreeViewItem {id = pose.id, displayName = pose.name};
            parentMap[poseItem.id] = poseItem;
            poseMap[pose.id] = pose;
        }

        foreach(var pose in _handle.poses)
        {
            if (pose == null) continue;
            if(pose.parentId <= 0)
                root.AddChild(parentMap[pose.id]);
            else
                parentMap[pose.parentId].AddChild(parentMap[pose.id]);
        }

        if (root.children == null || root.children.Count <= 0)
        {
            var pose = _handle.SavePose(-1);
            var poseItem = new TreeViewItem {id = pose.id, displayName = pose.name};
            root.AddChild(poseItem);
            parentMap[poseItem.id] = poseItem;
            poseMap[pose.id] = pose;
        }
        
        SetupDepthsFromParentsAndChildren(root);
        return root;
    }

    protected override void SelectionChanged(IList<int> selectedIds)
    {
        base.SelectionChanged(selectedIds);
        if (selectedIds.Count > 0)
        {
            _handle.LoadPose(selectedIds[0]);
        }
    }

    protected override bool CanRename(TreeViewItem item)
    {
        return true;
    }

    protected override void RenameEnded(RenameEndedArgs args)
    {
        _handle.poses.Find(a => a.id == args.itemID).name = args.newName;
        Reload();
    }

    protected override bool CanStartDrag(CanStartDragArgs args)
    {
        return true;
    }

    protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
    {
        DragAndDrop.PrepareStartDrag();
        DragAndDrop.SetGenericData("Pose", args.draggedItemIDs);
        DragAndDrop.StartDrag("Pose");
    }

    protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
    {
        if (args.performDrop)
        {
            var draggedItemIds = DragAndDrop.GetGenericData("Pose") as List<int>;
            var parentId = args.parentItem.id;
            foreach (var id in draggedItemIds)
            {
                poseMap[id].parentId = parentId;
            }
            Reload();
            return DragAndDropVisualMode.None;
        }
        return DragAndDropVisualMode.Move;
    }

    protected override bool CanBeParent(TreeViewItem item)
    {
        return true;
    }

    protected override void RowGUI(RowGUIArgs args)
    {
        base.RowGUI(args);
        var item = args.item;
        
        var rect = args.rowRect;
        rect.x = rect.width - 32;
        rect.width = 24;
        if (GUI.Button(rect, "+"))
        {
            _handle.SavePose(item.id);
            Reload();
        }
        
    }
}


[CustomEditor(typeof(FkHandle))]
public class FkHandleEditor : Editor
{
    private List<Transform> transforms = new List<Transform>();
    private Transform hotTransform;
    

    private PoseTreeView _poseTreeView;
    [SerializeField] private TreeViewState _treeViewState;
    
    void OnEnable()
    {
        _treeViewState = new TreeViewState();
        _poseTreeView = new PoseTreeView(_treeViewState, target as FkHandle);
        transforms.Clear();
        foreach (var i in Selection.GetFiltered<FkHandle>(SelectionMode.TopLevel))
        {
            foreach(var t in i.bones)
                if(t.childCount > 0)
                    transforms.Add(t);
        }
    }


    public override void OnInspectorGUI()
    {
        var fkh = target as FkHandle;
        if (GUILayout.Button("Reset"))
        {
            fkh.LoadPose(0);
        }
        
        base.OnInspectorGUI();
        
        var inspectorWidth = EditorGUIUtility.currentViewWidth;
         
        var rect = GUILayoutUtility.GetRect(inspectorWidth, inspectorWidth, 0, 1000);
        _poseTreeView?.OnGUI(rect);
        for(var i=fkh.poses.Count-1; i>=0; i--)
        {
            // if (GUILayout.Button(fkh.poses[i].name))
            // {
            //     fkh.LoadPose(i);
            // }
        }
        
    }

    private void OnSceneGUI()
    {
        using (var cc = new EditorGUI.ChangeCheckScope())
        {
            OnToolGUI(SceneView.lastActiveSceneView);
            if (cc.changed)
            {
                Undo.RecordObject(target, "Changed Look At Position");
                var fkh = target as FkHandle;
                fkh.Update();
            }
        }
    }

    public void OnToolGUI(EditorWindow window)
    {
        if (!(window is SceneView sceneView))
            return;
        
        var fkh = target as FkHandle;
        
        var mousePos = Event.current.mousePosition;

        var excludeTransforms = new HashSet<Transform>();
        if(fkh.enableLookAt)
            excludeTransforms.UnionWith(fkh.lookAtBones);
        
        foreach (var t in transforms)
        {
            if(excludeTransforms.Contains(t)) continue;
            if(t.parent == null) continue;

            var guiPos = HandleUtility.WorldToGUIPoint(t.position);
            var distance = Vector2.Distance(mousePos, guiPos);
            var size = HandleUtility.GetHandleSize(t.position)*0.1f;
            var rotation = Camera.current.transform.rotation;

            if(t.parent != null)
            {
                //Handles.DrawLine(t.position, t.GetChild(i).position, size);
                var childPosition = t.parent.position;
                var left = t.TransformDirection(Vector3.left) * size;
                Handles.DrawAAPolyLine(2, t.position, childPosition);
            }

            if (Handles.Button(t.position, rotation, size, size, Handles.CircleHandleCap))
            {
                if(Event.current.button == 0)
                {
                    if (hotTransform == t)
                        hotTransform = null;
                    else
                    {
                        hotTransform = t;
                        SceneView.currentDrawingSceneView.ShowNotification(new GUIContent(hotTransform.name), 1);
                    }
                }

                if (Event.current.shift)
                {
                    HandleContextClick(t);
                }
            }
        }
        
        if(hotTransform != null)
        {
            var newRotation = Handles.RotationHandle(hotTransform.rotation, hotTransform.position);
            if (newRotation != hotTransform.rotation)
            {
                Undo.RecordObject(hotTransform, "Rotate");
                hotTransform.rotation = newRotation;
            }
        }
        

        if (fkh.enableLookAt)
        {
            fkh.lookAtPosition = Handles.PositionHandle(fkh.lookAtPosition, fkh.transform.rotation);
            var lastBone = fkh.lookAtBones[^1];
            
        }
    }

    private void HandleContextClick(Transform transform)
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent($"{transform.name}"), false, () => { });
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Lock Position"), false, () =>
        {
            
        });
        menu.ShowAsContext();
    }
}
