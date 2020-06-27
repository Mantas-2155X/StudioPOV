using System.Collections;

using Studio;
using AIChara;

using HarmonyLib;

using BepInEx;
using BepInEx.Configuration;

using UnityEngine;

namespace HS2_StudioPOV
{
    [BepInProcess("StudioNEOV2")]
    [BepInPlugin(nameof(HS2_StudioPOV), nameof(HS2_StudioPOV), VERSION)]
    public class HS2_StudioPOV : BaseUnityPlugin
    {
        public const string VERSION = "1.1.1";
        
        private static EyeObject[] eyes;
        private static ChaControl chara;
        private static GameObject head;
        
        private static Studio.CameraControl cc;
        private static Studio.CameraControl.CameraData backupData;
        
        private static Studio.Studio studio;
        
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
            (fov = Config.Bind(new ConfigDefinition("General", "FOV"), 75f, new ConfigDescription("POV field of view", new AcceptableValueRange<float>(1f, 180f)))).SettingChanged += delegate
            {
                if (!toggle || cc == null)
                    return;

                cc.fieldOfView = fov.Value;
            };
            (hideHead = Config.Bind(new ConfigDefinition("General", "Hide head"), true)).SettingChanged += delegate
            {
                if (!toggle || head == null)
                    return;

                head.SetActive(!hideHead.Value);
            };
            
            var harmony = new Harmony(nameof(HS2_StudioPOV));
            harmony.PatchAll(typeof(HS2_StudioPOV));
        }

        private void Update()
        {
            if (togglePOV.Value.IsDown())
            {
                if (studio == null || cc == null)
                {
                    studio = Singleton<Studio.Studio>.Instance;
                    if (studio == null)
                        return;
                    
                    cc = studio.cameraCtrl;
                    if (cc == null)
                        return;
                }

                if (!toggle)
                    StartPOV();
                else
                    StopPOV();
            }

            if (!toggle) 
                return;

            if (chara == null)
            {
                StopPOV();
                return;
            }
            
            if (Input.GetKey(KeyCode.Mouse0))
            {
                var x = Input.GetAxis("Mouse X") * sensitivity.Value;
                var y = -Input.GetAxis("Mouse Y") * sensitivity.Value;
                
                viewRotation += new Vector3(y, x, 0f);
            }

            StartCoroutine(ApplyPOV());
        }

        private static IEnumerator ApplyPOV()
        {
            yield return new WaitForEndOfFrame();

            chara.neckLookCtrl.neckLookScript.aBones[0].neckBone.Rotate(viewRotation);
            
            cc.targetPos = Vector3.Lerp(eyes[0].eyeTransform.position, eyes[1].eyeTransform.position, 0.5f);
            cc.cameraAngle = eyes[0].eyeTransform.eulerAngles;
        }
        
        private static void StartPOV()
        {
            var ctrlInfo = Studio.Studio.GetCtrlInfo(Singleton<Studio.Studio>.Instance.treeNodeCtrl.selectNode);
            if (!(ctrlInfo is OCIChar ocichar))
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

            cc.fieldOfView = fov.Value;

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