using Studio;
using AIChara;

using HarmonyLib;

using BepInEx;
using BepInEx.Harmony;
using BepInEx.Configuration;

using UnityEngine;

namespace AI_StudioPOV
{
    [BepInPlugin(nameof(AI_StudioPOV), nameof(AI_StudioPOV), VERSION)][BepInProcess("StudioNEOV2")]
    public class AI_StudioPOV : BaseUnityPlugin
    {
        public const string VERSION = "1.0.0";

        private static EyeObject[] eyes;
        private static ChaControl chara;
        private static GameObject head;
        
        private static Studio.CameraControl cc;
        private static Studio.CameraControl.CameraData backupData;
        
        private static Vector3 viewRotation;
        
        private static float backupFov;
        private static bool toggle;

        private static ConfigEntry<KeyboardShortcut> togglePOV { get; set; }
        private static ConfigEntry<bool> hideHead { get; set; }
        private static ConfigEntry<float> fov { get; set; }
        private static ConfigEntry<float> sensitivity { get; set; }

        private void Awake()
        {
            togglePOV = Config.Bind("Keyboard Shortcuts", "Toggle POV", new KeyboardShortcut(KeyCode.P));
            
            sensitivity = Config.Bind(new ConfigDefinition("General", "Mouse sensitivity"), 2f);
            fov = Config.Bind(new ConfigDefinition("General", "FOV"), 75f, new ConfigDescription("POV field of view", new AcceptableValueRange<float>(1f, 180f)));
            hideHead = Config.Bind(new ConfigDefinition("General", "Hide head"), true);

            hideHead.SettingChanged += delegate
            {
                if (!toggle || cc == null || head == null)
                    return;

                head.SetActive(!hideHead.Value);
            };
            
            HarmonyWrapper.PatchAll(typeof(AI_StudioPOV));
        }

        private void LateUpdate()
        {
            if (togglePOV.Value.IsDown())
            {
                if (!Singleton<Studio.Studio>.IsInstance())
                    return;
                
                if (!toggle)
                    StartPOV();
                else
                    StopPOV();
            }

            if (!toggle) 
                return;

            if (chara == null)
                StopPOV();

            if (Input.GetKey(KeyCode.Mouse0))
            {
                var x = Input.GetAxis("Mouse X") * sensitivity.Value;
                var y = -Input.GetAxis("Mouse Y") * sensitivity.Value;
                
                viewRotation += new Vector3(y, x, 0f);
            }
            
            ApplyPOV();
        }

        private static void ApplyPOV()
        {
            chara.neckLookCtrl.neckLookScript.aBones[0].neckBone.Rotate(viewRotation);

            cc.targetPos = Vector3.Lerp(eyes[0].eyeTransform.position, eyes[1].eyeTransform.position, 0.5f);
            cc.cameraAngle = eyes[0].eyeTransform.eulerAngles;
            cc.fieldOfView = fov.Value;
        }
        
        private static void StartPOV()
        {
            var ctrlInfo = Studio.Studio.GetCtrlInfo(Singleton<Studio.Studio>.Instance.treeNodeCtrl.selectNode);
            if (!(ctrlInfo is OCIChar ocichar))
                return;
            
            var temp = GameObject.Find("StudioScene/Camera/CameraSet/CameraController");
            if (temp == null)
                return;
            
            cc = temp.GetComponent<Studio.CameraControl>();
            if (cc == null)
                return;
            
            chara = ocichar.charInfo;
            
            eyes = chara.eyeLookCtrl.eyeLookScript.eyeObjs;
            if (eyes == null)
                return;
            
            head = chara.objHeadBone;
            if (head == null)
                return;

            if(hideHead.Value)
                head.SetActive(false);

            var data = cc.Export();
            
            backupData = data;
            backupFov = cc.fieldOfView;
            
            cc.Import(new Studio.CameraControl.CameraData(data) {distance = Vector3.zero});
            viewRotation = Vector3.zero;

            toggle = true;
        }

        private static void StopPOV()
        {
            if (cc != null && backupData != null)
            {
                cc.Import(backupData);
                cc.fieldOfView = backupFov;
            }

            if(head != null)
                head.SetActive(true);

            chara = null;
            eyes = null;
            backupData = null;
            toggle = false;
        }
        
        [HarmonyPrefix, HarmonyPatch(typeof(Studio.CameraControl), "LateUpdate")]
        private static bool CameraControl_LateUpdate_Patch()
        {
            return !toggle;
        }
    }
}