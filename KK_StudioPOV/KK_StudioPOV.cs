using System.Collections;

using Studio;

using HarmonyLib;

using BepInEx;
using BepInEx.Configuration;

using UnityEngine;
using static System.Runtime.CompilerServices.RuntimeHelpers;
using static GameCursor;
using System;
using static UnityEngine.GUI;

namespace KK_StudioPOV
{
    [BepInProcess("CharaStudio")]
    [BepInPlugin(nameof(KK_StudioPOV), nameof(KK_StudioPOV), VERSION)]
    public class KK_StudioPOV : BaseUnityPlugin
    {
        public const string GUID = "com.2155X.bepinex.studiopov";

        public const string VERSION = "1.1.2";
        
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
        private static ConfigEntry<KeyboardShortcut> dragKey { get; set; }

        private static ConfigEntry<bool> hideHead { get; set; }
        private static ConfigEntry<float> fov { get; set; }
        private static ConfigEntry<float> sensitivity { get; set; }

        private void Awake()
        {
            togglePOV = Config.Bind("Keyboard Shortcuts", "Toggle POV", new KeyboardShortcut(KeyCode.P));
            dragKey = Config.Bind("Keyboard Shortcuts", "Drag key", new KeyboardShortcut(KeyCode.Mouse2), new ConfigDescription("Hold this key to drag the camera around."));

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

            var harmony = new Harmony(nameof(KK_StudioPOV));
            harmony.PatchAll(typeof(KK_StudioPOV));
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

            if (dragKey.Value.IsPressed())
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
        
        private readonly int uiWindowHash = GUID.GetHashCode();
        private Rect uiRect = new Rect(20, Screen.height / 2 - 150, 160, 223);

        protected void OnGUI()
        {
            if (toggle)
            {
                uiRect = GUILayout.Window(uiWindowHash, uiRect, WindowFunction, "POV settings");
            }
        }
        private void WindowFunction(int windowID)
        {
            // Resolution settings section
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("FOV", GUI.skin.label);
                GUILayout.BeginHorizontal();
                {
                    fov.Value = Mathf.Round(GUILayout.HorizontalSlider(fov.Value, 1f, 180f) * 10) / 10;
                    GUILayout.Label(fov.Value.ToString("0.0"), GUI.skin.label, GUILayout.ExpandWidth(false));
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUI.DragWindow();
        }
        
        [HarmonyPrefix, HarmonyPatch(typeof(Studio.CameraControl), "LateUpdate")]
        private static bool CameraControl_LateUpdate_Patch(Studio.CameraControl __instance)
        {
            if (!toggle) 
                return true;
            
            Traverse.Create(__instance).Method("CameraUpdate").GetValue();
            
            return false;
        }
    }
}