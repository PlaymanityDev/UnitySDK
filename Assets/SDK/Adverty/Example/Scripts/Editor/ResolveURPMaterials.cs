using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using UnityEngine.Rendering;
using System.IO;
using System.Linq;

[InitializeOnLoad]
public static class ResolveURPMaterials
{
    private const string PetrolStationMaterials = "Example/PetrolStation/Materials/";
    private const string UNIVERSAL_PIPELINE = "UniversalRenderPipeline";
    private const string URP_SHADER_FEATURE = "URP_INCLUDED";

    private static RenderPipelineAsset currentPipelineAsset = null;

    static ResolveURPMaterials()
    {
        if(GraphicsSettings.renderPipelineAsset == null)
        {
            ConfigurateMaterials(false);
        }

        EditorApplication.update += OnUpdate;
    }

    private static void OnUpdate()
    {
        ProcessActivePipeline();
    }

    private static void ProcessActivePipeline()
    {
        if (GraphicsSettings.renderPipelineAsset != currentPipelineAsset)
        {
            if(GraphicsSettings.renderPipelineAsset == null)
            {
                currentPipelineAsset = null;
                ConfigurateMaterials(false);
                return;
            }

            currentPipelineAsset = GraphicsSettings.renderPipelineAsset;
            string srpType = GraphicsSettings.renderPipelineAsset.GetType().ToString();
            if (srpType.Contains(UNIVERSAL_PIPELINE))
            {
                ConfigurateMaterials(true);
            }
        }
    }

    private static void ConfigurateMaterials(bool enableURPSupport)
    {
        string exampleMaterialsPath = Adverty.Editor.EditorUtils.AdvertyDirectory + PetrolStationMaterials;
        
        if(!Directory.Exists(exampleMaterialsPath))
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:material", new string[]{exampleMaterialsPath});
        List<Material> allMaterials = new List<Material>();

        foreach(string guid in guids)
        {
            allMaterials.Add(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        foreach(Material material in allMaterials)
        {
            if(enableURPSupport)
            {
                material.EnableKeyword(URP_SHADER_FEATURE);
            }
            else
            {
                material.DisableKeyword(URP_SHADER_FEATURE);
            }
        }
    }
}
