using System.Collections.Generic;
using Didimo.Networking.DataObjects;
using UnityEngine;
using System.IO;
using Unity.Collections;
using UnityEngine.Animations;
using Didimo.Utils;
using Didimo.Utils.Serialization;
using System;
using Didimo.Animation.AnimationPlayer;
using Didimo.Animation;
using Didimo.Animation.DataObjects;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Didimo.DidimoManagement
{
    /// <summary>
    /// The DidimoModel class, which we will create after parsing the model json that is generated by our pipeline. <see cref="DidimoModelFactory.CreateDidimoModel(Networking.DataObjects.DidimoModelDataObject)"/>
    /// This class is then used to create the didimo model, with skin weights, materials, etc.
    /// </summary>
    public class DidimoModel2 : DidimoModel<DidimoModelDataObject2>
    {
        public DidimoModel2(DidimoModelDataObject2 dataObject) : base(dataObject)
        {
        }

        Didimo.Animation.RealtimeRig AddRealtimeRigComponent(Transform root)
        {
            Didimo.Animation.RealtimeRig realtimeRig = root.GetComponent<Didimo.Animation.RealtimeRig>();

            if (!string.IsNullOrEmpty(dataObject.realtime_rig))
            {
                // If the realtime rig component doesn't exist, create it
                if (realtimeRig == null)
                {
                    realtimeRig = root.gameObject.AddComponent<Didimo.Animation.RealtimeRig>();
                }
            }

            return realtimeRig;
        }

        void SetRealtimeRig(Didimo.Animation.RealtimeRig realtimeRig, string modelAssetsPath)
        {
            if (!string.IsNullOrEmpty(dataObject.realtime_rig))
            {
                StreamReader reader = new StreamReader(Path.Combine(modelAssetsPath, dataObject.realtime_rig));
                RealtimeRigDataObject reatimeRigDO = MiniJSON.Deserialize<RealtimeRigDataObject>(reader.ReadToEnd());
                reader.Close();
                realtimeRig.Build(reatimeRigDO);
            }
            else
            {
                // If realtime rig exists, but the new didimo doesn't support it, clear its data
                if (realtimeRig != null)
                {
                    realtimeRig.realtimeRigAvatar = ScriptableObject.CreateInstance<RealtimeRigAvatar>();
                }
            }
        }

        public override void InstantiateDidimo(Transform root, string modelAssetsPath, bool updateOnly)
        {
            base.InstantiateDidimo(root, modelAssetsPath, updateOnly);

            Didimo.Animation.RealtimeRig realtimeRig;

            if (!updateOnly)
            {
                realtimeRig = AddRealtimeRigComponent(root);
                RealtimeRigAvatar realtimeRigAvatar = ScriptableObject.CreateInstance<RealtimeRigAvatar>();
                if(realtimeRig!=null)
                    realtimeRig.realtimeRigAvatar = realtimeRigAvatar;
            }
            else
            {
                realtimeRig = root.GetComponent<Didimo.Animation.RealtimeRig>();
            }

            SetRealtimeRig(realtimeRig, modelAssetsPath);

            if (root.GetComponent<AnimationPlayer>() == null)
            {
                root.gameObject.AddComponent<AnimationPlayer>();
            }
        }

        void CreateHierarchy(bool updateOnly, DidimoModelDataObject2.Node2 node, Transform parent)
        {
            GameObject go;
            if (updateOnly)
            {
                go = parent.Find(node.name).gameObject;
                if (go == null)
                {
                    throw (new System.Exception("Failed to get transform named " + node.name + ", when creating the didimo hierarchy."));
                }
            }
            else
            {
                go = new GameObject();
                go.name = node.name;
                go.transform.parent = parent;
            }
            SetTransformValues(node, go.transform);

            if (node.children != null)
            {
                foreach (DidimoModelDataObject2.Node2 child in node.children)
                {
                    CreateHierarchy(updateOnly, child, go.transform);
                }
            }
        }

        protected override void CreateHierarchy(bool updateOnly)
        {
            CreateHierarchy(updateOnly, dataObject.root_node, transform);
        }

        public override bool MeshHasSkinning(string meshName)
        {
            foreach (DidimoModelDataObject2.Mesh2 mesh in dataObject.meshes)
            {
                if (mesh.name == meshName)
                {
                    return mesh.skin_indices != null && mesh.skin_indices.Count > 0;
                }
            }

            Debug.LogError("Failed to find mesh " + meshName);
            return false;
        }

        protected override void SetConstraints()
        {
            foreach (DidimoModelDataObject2.Constraint constraint in dataObject.constraints)
            {
                Transform constrainedObj = transform.FindRecursive(constraint.constrainedObj);
                ConstraintSource constraintSource = new ConstraintSource();
                constraintSource.sourceTransform = transform.FindRecursive(constraint.constraintSrc);
                constraintSource.weight = 1;

                switch (constraint.type)
                {
                    case "parent":
                        {
                            ParentConstraint unityConstraint = null;
                            if (updateOnly)
                            {
                                unityConstraint = constrainedObj.gameObject.GetComponent<ParentConstraint>();
                            }
                            else
                            {
                                unityConstraint = constrainedObj.gameObject.AddComponent<ParentConstraint>();
                            }
                            unityConstraint.weight = 1;

                            Vector3 positionOffset = constraintSource.sourceTransform.InverseTransformPoint(constrainedObj.position);
                            Quaternion rotationOffset = Quaternion.Inverse(constraintSource.sourceTransform.rotation) * constrainedObj.rotation;

                            List<ConstraintSource> sources = new List<ConstraintSource>();
                            sources.Add(constraintSource);
                            unityConstraint.SetSources(sources);

                            unityConstraint.SetTranslationOffset(0, positionOffset);
                            unityConstraint.SetRotationOffset(0, rotationOffset.eulerAngles);

                            unityConstraint.constraintActive = true;
                            unityConstraint.locked = true;
                        }
                        break;
                    case "position":
                        {
                            PositionConstraint unityConstraint = null;
                            if (updateOnly)
                            {
                                unityConstraint = constrainedObj.gameObject.GetComponent<PositionConstraint>();
                            }
                            else
                            {
                                unityConstraint = constrainedObj.gameObject.AddComponent<PositionConstraint>();
                            }
                            unityConstraint.weight = 1;

                            Vector3 positionOffset = constraintSource.sourceTransform.InverseTransformPoint(constrainedObj.position);

                            List<ConstraintSource> sources = new List<ConstraintSource>();
                            sources.Add(constraintSource);
                            unityConstraint.SetSources(sources);

                            unityConstraint.translationOffset = positionOffset;

                            unityConstraint.constraintActive = true;
                            unityConstraint.locked = true;
                        }
                        break;
                    default:
                        Debug.LogError("Constraint type not supported yet: " + constraint.type);
                        break;
                }
            }
        }

        protected override void SetMaterials()
        {
            Dictionary<string, Texture2D> textures = dataObject.material_state.GetAllTextures(modelSourcePath);
            dataObject.material_state.SetAllProperties(modelSourcePath, textures, transform);
        }


        public override void SetBoneWeights()
        {
            foreach (DidimoModelDataObject2.Mesh2 meshDO in dataObject.meshes)
            {
                if (meshDO.skin_indices == null || meshDO.skin_indices.Count == 0)
                {
                    continue;
                }
                // Sanity check
                if (meshDO.skin_indices.Count != meshDO.skin_weights.Count)
                {
                    Debug.LogError("Skin indices and skinWeights counts don't match!");
                    return;
                }

                byte[] bonesPerVertex = new byte[meshDO.skin_indices.Count];
                List<BoneWeight1> boneWeights = new List<BoneWeight1>();

                for (int vertId = 0; vertId < meshDO.skin_indices.Count; vertId++)
                {
                    if (meshDO.skin_weights[vertId].Count >= 255)
                    {
                        Debug.LogError("ERROR: exceeded maximum number of weights: " + meshDO.skin_weights[vertId].Count);
                    }
                    bonesPerVertex[vertId] = (byte)meshDO.skin_weights[vertId].Count;

                    for (int weightId = 0; weightId < meshDO.skin_weights[vertId].Count; weightId++)
                    {
                        BoneWeight1 boneWeight = new BoneWeight1();
                        boneWeight.boneIndex = meshDO.skin_indices[vertId][weightId];
                        boneWeight.weight = meshDO.skin_weights[vertId][weightId];

                        boneWeights.Add(boneWeight);
                    }
                }

                var bonesPerVertexArray = new NativeArray<byte>(bonesPerVertex, Allocator.Temp);
                var weightsArray = new NativeArray<BoneWeight1>(boneWeights.ToArray(), Allocator.Temp);

                SkinnedMeshRenderer smr = transform.FindRecursive(meshDO.name).GetComponent<SkinnedMeshRenderer>();
                // set boneWeights to null, otherwise we might get an error when calling SetBoneWeights
                smr.sharedMesh.boneWeights = null;
                smr.sharedMesh.SetBoneWeights(bonesPerVertexArray, weightsArray);

                Transform[] bones = new Transform[dataObject.bones.Count];

                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i] = transform.FindRecursive(dataObject.bones[i]);
                }
                smr.bones = bones;

                smr.localBounds = smr.sharedMesh.bounds;

                Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    bindPoses[i] = bones[i].worldToLocalMatrix * smr.transform.localToWorldMatrix;
                }
                smr.sharedMesh.bindposes = bindPoses;
            }
        }

#if UNITY_EDITOR

        public override void ImportDidimo(string didimoName, Transform root, string sourcePath, string targetPath)
        {
            base.ImportDidimo(didimoName, root, sourcePath, targetPath);
            Didimo.Animation.RealtimeRig realtimeRig = AddRealtimeRigComponent(root);
            CreateRealtimeRigInstance(didimoName, realtimeRig);
            SetRealtimeRig(realtimeRig, sourcePath);

            // Also add the animation player component
            if (root.GetComponent<AnimationPlayer>() == null)
            {
                root.gameObject.AddComponent<AnimationPlayer>();
            }

            if(realtimeRig  !=  null)
                EditorUtility.SetDirty(realtimeRig.realtimeRigAvatar);
            SavePrefab();
        }

        void CreateRealtimeRigInstance(string didimoName, Didimo.Animation.RealtimeRig realtimeRig)
        {
            string modelTargetRelativePath = modelTargetPath;
            if (modelTargetRelativePath.StartsWith(Application.dataPath))
            {
                modelTargetRelativePath = "Assets" + modelTargetRelativePath.Substring(Application.dataPath.Length);
            }

            string realtimeRigAvatarPath = Path.Combine(modelTargetRelativePath, didimoName + "_realtimeRigAvatar.asset");

            RealtimeRigAvatar realtimeRigAvatar = AssetDatabase.LoadAssetAtPath<RealtimeRigAvatar>(realtimeRigAvatarPath);
            if (realtimeRigAvatar == null)
            {
                realtimeRigAvatar = ScriptableObject.CreateInstance<RealtimeRigAvatar>();
                AssetDatabase.CreateAsset(realtimeRigAvatar, realtimeRigAvatarPath);
            }
            else if (realtimeRigAvatarPath == null)
            {
                Debug.LogError("Failed to create realtime rig avatar at " + realtimeRigAvatarPath);
            }

            if(realtimeRig != null)
                realtimeRig.realtimeRigAvatar = realtimeRigAvatar;
        }

        protected override void ImportTextures()
        {
            string modelTargetRelativePath = modelTargetPath;
            if (modelTargetRelativePath.StartsWith(Application.dataPath))
            {
                modelTargetRelativePath = "Assets" + modelTargetRelativePath.Substring(Application.dataPath.Length);
            }

            HashSet<Tuple<string, bool>> textureNames = dataObject.material_state.GetAllTextureNames();
            foreach (Tuple<string, bool> textureName in textureNames)
            {
                byte[] textureBytes = File.ReadAllBytes(Path.Combine(modelSourcePath, textureName.Item1));

                string textureDestinationPath = Path.Combine(modelTargetRelativePath, textureName.Item1);
                File.WriteAllBytes(textureDestinationPath, textureBytes);

                AssetDatabase.ImportAsset(textureDestinationPath, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter textureImporter = AssetImporter.GetAtPath(textureDestinationPath) as TextureImporter;
                // If normal map
                if (textureName.Item2)
                {
                    textureImporter.sRGBTexture = false;
                }
                else
                {
                    textureImporter.sRGBTexture = true;
                }
                textureImporter.compressionQuality = 100;
                //AssetDatabase.ImportAsset(textureDestinationRelativePath);
            }
            AssetDatabase.Refresh();
        }

        protected override void ImportMaterials()
        {
            string modelTargetRelativePath = modelTargetPath;
            if (modelTargetRelativePath.StartsWith(Application.dataPath))
            {
                modelTargetRelativePath = "Assets" + modelTargetRelativePath.Substring(Application.dataPath.Length);
            }

            // First, load the textures (once), and store them in a map
            // A texture may be shared by multiple textures, and we don't want to load the same texture multiple times
            HashSet<Tuple<string, bool>> textureNames = dataObject.material_state.GetAllTextureNames();
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

            foreach (Tuple<string, bool> textureName in textureNames)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(modelTargetRelativePath, textureName.Item1));
                textures[textureName.Item1] = texture;
            }

            dataObject.material_state.SetAllProperties(modelTargetRelativePath, textures, transform, true);
        }
#endif
    }
}