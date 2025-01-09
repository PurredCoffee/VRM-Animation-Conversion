using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

// converts an anmation from one set of bones to another
public class FolderAssetProcessor : EditorWindow
{
    /* -------------------------------------------------------------------------- */
    /*                                     GUI                                    */
    /* -------------------------------------------------------------------------- */
    
    private string originalFolder = "";
    private string newFolder = "";
    private Animator animationDonor;
    private Animator animationTarget;
    private Transform boneDonor;
    private Transform boneTarget;
    private float positionAccuracy;
    private float rotationAccuracy;

    static string previousOriginalFolder;
    static string previousNewFolder;
    static Animator previousAnimationDonor;
    static Animator previousAnimationTarget;
    static Transform previousBoneDonor;
    static Transform previousBoneTarget;
    static float previousPositionAccuracy;
    static float previousRotationAccuracy;

    /// <summary>
    /// This function is used to create the window, it tells Unity to add a new menu item and sets its response to open the window
    /// </summary>
    [MenuItem("Tools/Transmute Animations")]
    public static void ShowWindow()
    {
        GetWindow<FolderAssetProcessor>("Transmute Animations");
    }

    /// <summary>
    /// We set the values to what they were before the window was closed
    /// </summary>
    public void Awake() {
        originalFolder = previousOriginalFolder;
        newFolder = previousNewFolder;
        animationDonor = previousAnimationDonor;
        animationTarget = previousAnimationTarget;
        boneDonor = previousBoneDonor;
        boneTarget = previousBoneTarget;
        positionAccuracy = previousPositionAccuracy;
        rotationAccuracy = previousRotationAccuracy;
    }
    
    /// <summary>
    /// GUI function, gets parameters from the user and with some jank verifies if they are set correctly
    /// </summary>
    private void OnGUI()
    {
        GUILayout.Label("0.2.0 - Made by katzentatzentanz, with love and paws");
        GUILayout.Space(10);

        animationDonor = EditorGUILayout.ObjectField("Donor Animation", animationDonor, typeof(Animator), true) as Animator;
        boneDonor = EditorGUILayout.ObjectField("Donor Bone", boneDonor, typeof(Transform), true) as Transform;
        //check if the bone is a child of the animation object
        if(boneDonor != null && !boneDonor.IsChildOf(animationDonor.transform)) {
            boneDonor = null;
            Debug.LogError("Bone must be a child of the donor animation object");
        }
        if(boneDonor == null) {
            EditorGUILayout.LabelField("Bone must be a child of the donor animation object");
        }
        GUILayout.Space(5);

        animationTarget = EditorGUILayout.ObjectField("Target Animation", animationTarget, typeof(Animator), true) as Animator;
        boneTarget = EditorGUILayout.ObjectField("Target Bone", boneTarget, typeof(Transform), true) as Transform;
        //check if the bone is a child of the animation object
        if(boneTarget != null && !boneTarget.IsChildOf(animationTarget.transform)) {
            boneTarget = null;
            Debug.LogError("Bone must be a child of the target animation object");
        }
        if(boneTarget == null) {
            EditorGUILayout.LabelField("Bone must be a child of the target animation object");
        }
        
        GUILayout.Space(10);

        if (GUILayout.Button("Select Original Folder"))
        {
            //does some string manipulation to make sure the folder is inside the assets folder and is relative to the assets folder
            string selectedFolder = EditorUtility.OpenFolderPanel("Select Original Folder", Application.dataPath, "");
            if (!selectedFolder.StartsWith(Application.dataPath))
            {
                Debug.LogError("Folder must be inside the Assets folder");
                selectedFolder = "";
            }
            selectedFolder = selectedFolder.Replace(Application.dataPath + "/", "Assets/");
            selectedFolder = selectedFolder.Replace(Application.dataPath, "Assets/");
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                if(!selectedFolder.EndsWith("/")) {
                    selectedFolder += "/";
                }
                originalFolder = selectedFolder;
            }
        }
        GUILayout.Label("Original Folder: " + originalFolder);

        GUILayout.Space(5);

        if (GUILayout.Button("Select New Folder"))
        {
            //does the same thing as the previous if statement
            string selectedFolder = EditorUtility.OpenFolderPanel("Select New Folder", Application.dataPath, "");
            if (!selectedFolder.StartsWith(Application.dataPath))
            {
                Debug.LogError("Folder must be inside the Assets folder");
                selectedFolder = "";
            }
            selectedFolder = selectedFolder.Replace(Application.dataPath + "/", "Assets/");
            selectedFolder = selectedFolder.Replace(Application.dataPath, "Assets/");
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                if(!selectedFolder.EndsWith("/")) {
                    selectedFolder += "/";
                }
                newFolder = selectedFolder;
            }
        }
        GUILayout.Label("New Folder: " + newFolder);

        GUILayout.Space(10);

        //create a row of two float selectors for the accuracy of the position and rotation
        GUILayout.Label("Accuracy");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Position");
        positionAccuracy = EditorGUILayout.FloatField(positionAccuracy);
        GUILayout.Label("Rotation");
        rotationAccuracy = EditorGUILayout.FloatField(rotationAccuracy);
        GUILayout.EndHorizontal();
        positionAccuracy = Mathf.Max(0, positionAccuracy);
        rotationAccuracy = Mathf.Max(0, rotationAccuracy);
        GUILayout.Label("(higher for less keyframes but more jitter)");
        
        GUILayout.Space(10);

        //error handling
        if(animationDonor == null || animationTarget == null)
        {
            GUILayout.Label("Please select both animation objects");
        }
        if(boneDonor == null || boneTarget == null)
        {
            GUILayout.Label("Please select both bone objects");
        }
        if(string.IsNullOrEmpty(originalFolder) || string.IsNullOrEmpty(newFolder))
        {
            GUILayout.Label("Please select both folders");
        } else {
            if (!Directory.Exists(originalFolder))
            {
                GUILayout.Label("Folder does not exist: " + originalFolder);
            }
            if(!Directory.Exists(newFolder))
            {
                GUILayout.Label("Folder does not exist: " + newFolder);
            }
        }

        //this is where the magic happens
        Dictionary<Transform, Transform> lookupObjectTable = new Dictionary<Transform, Transform>();
        List<string> assetPaths = new List<string>();
        //if all the parameters are set correctly, we populate the lookup table
        if(animationDonor != null && animationTarget != null && boneDonor != null && boneTarget != null) {
            lookupObjectTable = new Dictionary<Transform, Transform>() {
                {boneDonor, boneTarget}
            };
            PopulateLookupTable(
                lookupObjectTable, 
                boneDonor.transform,
                boneTarget.transform,
                animationDonor.transform.position - animationTarget.transform.position
            );
        }
        //if the folders exist, we get the asset paths
        if(Directory.Exists(originalFolder) && Directory.Exists(newFolder)) {
            assetPaths = getAssets(originalFolder);
        }
        GUIStyle style = new GUIStyle(GUI.skin.button);
        //if the parameters are not set correctly, we disable the button
        if(assetPaths.Count == 0 || lookupObjectTable.Count == 0) {
            style.normal.textColor = Color.gray;
            style.active.textColor = Color.gray;
            style.hover.textColor = Color.gray;
        }
        if (GUILayout.Button("Process Assets", style) && assetPaths.Count > 0 && lookupObjectTable.Count > 0)
        {
            //duplicate the assets into the new folder
            List<AnimationClip> assets = createAssets(originalFolder, newFolder);
            //process the assets if 
            try {
                ProcessAssetsInFolder(
                    assets,
                    lookupObjectTable,
                    animationDonor,
                    animationTarget
                );
            } catch (System.Exception e) {
                Debug.LogError("Error processing assets: " + e.Message);
                Debug.LogError(e.StackTrace);
            }
        }

        GUILayout.Space(10);

        //debug information
        GUILayout.Label("Mismatched Bones:");
        foreach(Transform key in lookupObjectTable.Keys) {
            if(lookupObjectTable[key] == null) {
                GUILayout.Label(key.name + " -> null");
            } else if(lookupObjectTable[key].name != key.name) {
                GUILayout.Label(key.name + " -> " + lookupObjectTable[key].name);
            }
        }

        GUILayout.Space(10);

        GUILayout.Label("Assets:");
        foreach(string asset in assetPaths) {
            GUILayout.Label(asset);
        }

        //set cache
        previousAnimationDonor = animationDonor;
        previousAnimationTarget = animationTarget;
        previousBoneDonor = boneDonor;
        previousBoneTarget = boneTarget;
        previousOriginalFolder = originalFolder;
        previousNewFolder = newFolder;
        previousPositionAccuracy = positionAccuracy;
    }


    /* -------------------------------------------------------------------------- */
    /*                               Asset Handling                               */
    /* -------------------------------------------------------------------------- */

    /// <summary>
    /// This function is used to populate the lookup table for a specific bone. It finds the closest bone to the tartget in the list of children for a given donor bone
    /// </summary>
    /// <param name="branchToFind">The donor bone to find</param>
    /// <param name="rootToSearch">The target bone to look through</param>
    /// <param name="rootToRootOffset">The difference between the two objects since they will rarely be at the same position</param>
    /// <returns>Closest Bone that was found</returns>
    private static Transform findInChildren(Transform branchToFind, Transform rootToSearch, Vector3 rootToRootOffset) {
        /*
        basic minimum distance search for the closest bone in two trees

        e.g.

                     Test│A│B│C│D│E
            ─────────────┼─┼─┼─┼─┼─
                     Dist│8│5│7│1│9
            ─────────────┼─┼─┼─┼─┼─
            Smallest Dist│8│5│5│1│1
            ─────────────┼─┼─┼─┼─┼─
                 Best fit│A│B│B│D│D

        => D is the closest bone to the branch we are looking for
        */
        Transform closest = null;
        float minDist = float.MaxValue;
        for(int i = 0; i < rootToSearch.transform.childCount; i++) {
            Transform x = rootToSearch.transform.GetChild(i);
            if((x.transform.position-branchToFind.transform.position+rootToRootOffset).sqrMagnitude < minDist) {
                closest = x;
                minDist = (x.transform.position-branchToFind.transform.position+rootToRootOffset).sqrMagnitude;
            }
        }
        return closest;
    }

    /// <summary>
    /// This function is used to populate the lookup table for all the bones in the hierarchy it calls itself recursively to populate the table for all the children of the bones
    /// </summary>
    /// <param name="LookupTable">Reference to the Table that should be populated</param>
    /// <param name="DonorBone">Reference to the Donor Bone to look through</param>
    /// <param name="TargetBone">Reference to the Target Bone to look through</param>
    /// <param name="Offset">Offset between the original bones</param>
    private void PopulateLookupTable(Dictionary<Transform, Transform> LookupTable, Transform DonorBone, Transform TargetBone, Vector3 Offset) {
        //recursive function to basically just call findInChildren on every grand-child of the donor bone
        for(int i = 0; i < DonorBone.childCount; i++) {
            Transform p = DonorBone.GetChild(i);
            bool found = false;
            //if the bones have the same name it takes priority
            for(int j = 0; j < TargetBone.childCount; j++) {
                Transform n = TargetBone.GetChild(j);
                if(p.name == n.name) {
                    LookupTable[p] = n;
                    PopulateLookupTable(LookupTable, p, n, Offset);
                    found = true;
                    break;
                }
            }
            if(found) {
                continue;
            }
            //else find the closest bone
            Transform x = findInChildren(p, TargetBone, Offset);
            if(x != null) {
                LookupTable[p] = x;
                PopulateLookupTable(LookupTable, p, x, Offset);
            }
        }
    }

    /// <summary>
    /// Get all the assets in a folder that are AnimationClips
    /// </summary>
    /// <param name="path">The path to the folder</param>
    /// <returns>List of all paths to the AnimationClips in the folder</returns>
    private List<string> getAssets(string path) {
        string[] assetPaths = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly);
        List<string> assets = new List<string>();
        foreach (string assetPath in assetPaths)
        {
            AnimationClip checkAsset = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (checkAsset == null) {
                continue;
            }
            assets.Add(assetPath);
        }
        return assets;
    }

    /// <summary>
    /// Function to duplicate all the assets in a folder to a new folder and return a list of all the duplicated assets
    /// </summary>
    /// <param name="oldPath">The path to the original folder</param>
    /// <param name="newPath">The path to the new folder</param>
    /// <returns>List of all the duplicated assets</returns>
    private List<AnimationClip> createAssets(string oldPath, string newPath) {
        List<string> assetPaths = getAssets(oldPath);
        List<AnimationClip> assets = new List<AnimationClip>();

        foreach (string assetPath in assetPaths)
        {
            string newAssetPath = newPath + assetPath.Replace(oldPath, "").Replace(".anim","-VRM.anim");
            AssetDatabase.CopyAsset(assetPath, newAssetPath);

            AnimationClip asset = AssetDatabase.LoadAssetAtPath<AnimationClip>(newAssetPath);
            if (asset == null)
            {
                //should never happen
                continue;
            }
            assets.Add(asset);
        }
        return assets;
    }

    /* -------------------------------------------------------------------------- */
    /*                                    Math                                    */
    /* -------------------------------------------------------------------------- */

    //these are the names of the curves that we will insert into the new animation
    Dictionary<string, int> curveNames = new Dictionary<string, int> {
        {"m_LocalPosition.x", 0},
        {"m_LocalPosition.y", 1},
        {"m_LocalPosition.z", 2},
        {"m_LocalRotation.x", 3},
        {"m_LocalRotation.y", 4},
        {"m_LocalRotation.z", 5},
        {"m_LocalRotation.w", 6}
    };

    //these are the names of the curves that we will remove from the new animation
    Dictionary<string, int> originalCurveNames = new Dictionary<string, int> {
        {"m_LocalPosition.x", 0},
        {"m_LocalPosition.y", 1},
        {"m_LocalPosition.z", 2},
        {"m_LocalRotation.x", 3},
        {"m_LocalRotation.y", 4},
        {"m_LocalRotation.z", 5},
        {"m_LocalRotation.w", 6},
        {"m_LocalScale.x", 7},
        {"m_LocalScale.y", 8},
        {"m_LocalScale.z", 9}
    };

    /// <summary>
    /// This function is used to clean up the position curve, it removes redundant keys for the position curve
    /// </summary>
    /// <param name="curve">Curve to edit in place</param> 
    private void cleanupPosCurve(AnimationCurve curve) {
        if(curve.keys.Length < 2) {
            return;
        }
        float lastdiff = curve.keys[1].value - curve.keys[0].value;
        float lastvalue = curve.keys[1].value;
        for(int i = 2; i < curve.keys.Length; i++) {
            float diff = curve.keys[i].value - lastvalue;
            //if the difference hasn't changed the previous frame was redundant
            if(diff - lastdiff < positionAccuracy * positionAccuracy + 0.0001f) {
                curve.RemoveKey(i-1);
                i--;
                //set the tangents to be linear
                float slope = (curve.keys[i].value - curve.keys[i-1].value) / (curve.keys[i].time - curve.keys[i-1].time);
                Keyframe key = curve.keys[i];
                key.inTangent = slope;
                Keyframe prevkey = curve.keys[i-1];
                prevkey.outTangent = slope;
                curve.MoveKey(i, key);
                curve.MoveKey(i-1, prevkey);
            }
            lastdiff = diff;
            lastvalue = curve.keys[i].value;
        }
    }

    /// <summary>
    /// Cleans up rotation curves by removing unnecessary keyframes and setting slopes to be linear.
    /// </summary>
    /// <param name="objCurves">An array of AnimationCurve objects representing the rotation curves.</param>
    private void cleanupRotCurves(AnimationCurve[] objCurves)
    {
        if(objCurves[3].keys.Length < 2) {
            return;
        }
        //this is the first key
        Quaternion q1 = new Quaternion(objCurves[3].keys[0].value, objCurves[4].keys[0].value, objCurves[5].keys[0].value, objCurves[6].keys[0].value);
        //this is the second key
        Quaternion q2 = new Quaternion(objCurves[3].keys[1].value, objCurves[4].keys[1].value, objCurves[5].keys[1].value, objCurves[6].keys[1].value);

        for(int i = 2; i < objCurves[3].keys.Length; i++) {
            Quaternion q3 = new Quaternion(objCurves[3].keys[i].value, objCurves[4].keys[i].value, objCurves[5].keys[i].value, objCurves[6].keys[i].value);
            Quaternion q13 = Quaternion.Slerp(q1, q3, 0.5f);
            if((q3.eulerAngles - q13.eulerAngles).sqrMagnitude < rotationAccuracy * rotationAccuracy) {
                for(int o = 3; o < 7; o++) {
                    objCurves[o].RemoveKey(i-1);
                }
                i--;
                //set the slopes to be linear
                for(int o = 3; o < 7; o++) {
                    float slope = (objCurves[o].keys[i].value - objCurves[o].keys[i-1].value) / (objCurves[o].keys[i].time - objCurves[o].keys[i-1].time);
                    Keyframe key = objCurves[o].keys[i];
                    key.inTangent = slope;
                    Keyframe prevkey = objCurves[o].keys[i-1];
                    prevkey.outTangent = slope;
                    objCurves[o].MoveKey(i, key);
                    objCurves[o].MoveKey(i-1, prevkey);
                }
            }
            q1 = q2;
            q2 = q3;
        }
    }

    /// <summary>
    /// This function is used to process all the assets in a folder, it goes through all the assets and creates new curves for the new bones
    /// </summary>
    /// <param name="assets">List of all the assets to process</param>
    /// <param name="associatedObjects">Dictionary of all the bones and their new bones</param>
    /// <param name="animator1">The original animator</param>
    /// <param name="animator2">The new animator</param>
    private void ProcessAssetsInFolder(List<AnimationClip> assets, Dictionary<Transform, Transform> associatedObjects, Animator animator1, Animator animator2)
    {
        //store the original positions and rotations of the bones to reset them after processing
        Dictionary<Transform, Vector3> originalPositions1 = new Dictionary<Transform, Vector3>();
        Dictionary<Transform, Quaternion> originalRotations1 = new Dictionary<Transform, Quaternion>();
        Dictionary<Transform, Vector3> originalPositions2 = new Dictionary<Transform, Vector3>();
        Dictionary<Transform, Quaternion> originalRotations2 = new Dictionary<Transform, Quaternion>();
        Vector3 offsetPosition = animator2.transform.position - animator1.transform.position;
        foreach (Transform key in associatedObjects.Keys)
        {
            originalPositions1[key] = key.position;
            originalRotations1[key] = key.rotation;
            originalPositions2[key] = associatedObjects[key].position;
            originalRotations2[key] = associatedObjects[key].rotation;
        }
        //start the animation mode to sample the animations
        AnimationMode.StartAnimationMode();
        //go through all the assets
        foreach (AnimationClip asset in assets)
        {
            Debug.Log("Processing asset: " + asset.name);
            //create the new curves
            Dictionary<Transform, AnimationCurve[]> curves = new Dictionary<Transform, AnimationCurve[]>();
            Dictionary<Transform, Vector3> previousPositions = new Dictionary<Transform, Vector3>();
            Dictionary<Transform, int> PositionSetFrame = new Dictionary<Transform, int>();
            Dictionary<Transform, Quaternion> previousRotations = new Dictionary<Transform, Quaternion>();
            Dictionary<Transform, int> RotationSetFrame = new Dictionary<Transform, int>();
            foreach (Transform key in associatedObjects.Keys)
            {
                curves[key] = new AnimationCurve[] {
                    new AnimationCurve(),
                    new AnimationCurve(),
                    new AnimationCurve(),
                    new AnimationCurve(),
                    new AnimationCurve(),
                    new AnimationCurve(),
                    new AnimationCurve()
                };
            }
            //go through all frames
            for (int i = 0; i <= (int)(asset.length * asset.frameRate); i++)
            {
                AnimationMode.SampleAnimationClip(animator1.gameObject, asset, (float)i / asset.frameRate);
                //iterate through the keys of associatedObject backwards
                foreach(Transform key in associatedObjects.Keys)
                {
                    //copy the position and rotation of the original bone to the new bone
                    Quaternion rotationOffset = key.rotation * Quaternion.Inverse(originalRotations1[key]);
                    Quaternion globalRotation = originalRotations2[key] * rotationOffset;
                    associatedObjects[key].rotation = globalRotation;

                    Vector3 globalPosition = key.position + offsetPosition;
                    associatedObjects[key].position = globalPosition;
                }
                foreach (Transform key in associatedObjects.Keys)
                {
                    //check if the position or rotation has changed
                    //if its the first frame we check if the position is different from the original position
                    if((previousPositions.ContainsKey(key) && previousPositions[key] != associatedObjects[key].localPosition) ||
                        (!previousPositions.ContainsKey(key) && (originalPositions2[key] - associatedObjects[key].position).sqrMagnitude > positionAccuracy * positionAccuracy + 0.0001f)) {
                        //sometimes we need to prepend a frame before this
                        if(PositionSetFrame.ContainsKey(key) && PositionSetFrame[key] != i-1) {
                            curves[key][0].AddKey(new Keyframe((i-1) / asset.frameRate, previousPositions[key].x));
                            curves[key][1].AddKey(new Keyframe((i-1) / asset.frameRate, previousPositions[key].y));
                            curves[key][2].AddKey(new Keyframe((i-1) / asset.frameRate, previousPositions[key].z));
                        }
                        curves[key][0].AddKey(new Keyframe(i / asset.frameRate, associatedObjects[key].localPosition.x));
                        curves[key][1].AddKey(new Keyframe(i / asset.frameRate, associatedObjects[key].localPosition.y));
                        curves[key][2].AddKey(new Keyframe(i / asset.frameRate, associatedObjects[key].localPosition.z));
                        PositionSetFrame[key] = i;
                    }
                    previousPositions[key] = associatedObjects[key].localPosition;

                    //check if the rotation has changed
                    //if its the first frame we check if the rotation is different from the original rotation
                    if((previousRotations.ContainsKey(key) && previousRotations[key] != associatedObjects[key].localRotation) ||
                        (!previousRotations.ContainsKey(key) && originalRotations2[key] != associatedObjects[key].rotation)) {
                        //sometimes we need to prepend a frame before this
                        if(RotationSetFrame.ContainsKey(key) && RotationSetFrame[key] != i-1) {
                            curves[key][3].AddKey(new Keyframe((i-1) / asset.frameRate,  previousRotations[key].x));
                            curves[key][4].AddKey(new Keyframe((i-1) / asset.frameRate,  previousRotations[key].y));
                            curves[key][5].AddKey(new Keyframe((i-1) / asset.frameRate,  previousRotations[key].z));
                            curves[key][6].AddKey(new Keyframe((i-1) / asset.frameRate,  previousRotations[key].w));
                        }
                        curves[key][3].AddKey(new Keyframe(i / asset.frameRate, associatedObjects[key].localRotation.x));
                        curves[key][4].AddKey(new Keyframe(i / asset.frameRate, associatedObjects[key].localRotation.y));
                        curves[key][5].AddKey(new Keyframe(i / asset.frameRate, associatedObjects[key].localRotation.z));
                        curves[key][6].AddKey(new Keyframe(i / asset.frameRate, associatedObjects[key].localRotation.w));
                        RotationSetFrame[key] = i;
                    }
                    previousRotations[key] = associatedObjects[key].localRotation;
                }
                //reset the bones to their original positions
                foreach (Transform key in associatedObjects.Keys)
                {
                    associatedObjects[key].position = originalPositions2[key];
                    associatedObjects[key].rotation = originalRotations2[key];
                }
            }
            //apply the curves to the new animation
            foreach(Transform key in associatedObjects.Keys) {
                AnimationCurve[] objCurves = curves[key];
                EditorCurveBinding curveBinding = new EditorCurveBinding();
                curveBinding.type = typeof(Transform);
                curveBinding.path = AnimationUtility.CalculateTransformPath(key, animator1.transform);
                foreach(string curveName in originalCurveNames.Keys) {
                    curveBinding.propertyName = curveName;
                    AnimationUtility.SetEditorCurve(asset, curveBinding, null);
                }
                curveBinding.path = AnimationUtility.CalculateTransformPath(associatedObjects[key], animator2.transform);
                //rotations are cleaned up together while positions can be cleaned up individually
                cleanupRotCurves(objCurves);
                foreach(string curveName in curveNames.Keys) {
                    int index = curveNames[curveName];
                    curveBinding.propertyName = curveName;
                    if((RotationSetFrame.ContainsKey(key) && index >= 3) || (PositionSetFrame.ContainsKey(key) && index < 3)) {
                        //cleanup the position curve
                        if(index < 3) {
                            cleanupPosCurve(objCurves[index]);
                        }
                        AnimationUtility.SetEditorCurve(asset, curveBinding, objCurves[index]);
                    } else {
                        AnimationUtility.SetEditorCurve(asset, curveBinding, null);
                    }
                }
            }
            //log the total amount of curves
            Debug.Log("Processed " + asset.name + " with " + (RotationSetFrame.Keys.Count + PositionSetFrame.Keys.Count) + " curves");
        }
        AnimationMode.StopAnimationMode();
        AssetDatabase.SaveAssets();

		Resources.UnloadUnusedAssets();
        Debug.Log("Processed " + assets.Count + " assets");
    }
}
