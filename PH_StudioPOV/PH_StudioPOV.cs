using System.Collections;

using Studio;

using HarmonyLib;

using BepInEx;
using BepInEx.Configuration;

using UnityEngine;

namespace PH_StudioPOV
{
    [BepInProcess("PlayHomeStudio32bit")]
    [BepInProcess("PlayHomeStudio64bit")]
    [BepInPlugin(nameof(PH_StudioPOV), nameof(PH_StudioPOV), VERSION)]
    public class PH_StudioPOV : BaseUnityPlugin
    {
        public const string VERSION = "1.1.1";
        
        private static Transform[] eyes;
        private static GameObject head;
        private static ChaControl chara;
        
        private static CameraControl cc;
        private static CameraControl.CameraData backupData;

        private static Studio.Studio studio;
        
        private static float rotationX;
        private static float rotationY;
        
        private static float backupFov;
        private static bool toggle;

        private static ConfigEntry<KeyboardShortcut> togglePOV { get; set; }
        private static ConfigEntry<bool> hideHead { get; set; }
        private static ConfigEntry<float> fov { get; set; }
        private static ConfigEntry<float> sensitivity { get; set; }

        private void Awake()
        {
            togglePOV = Config.Bind("Keyboard Shortcuts", "Toggle POV", new KeyboardShortcut(KeyCode.P));
            
            sensitivity = Config.Bind(new ConfigDefinition("General", "Mouse sensitivity"), 80f);
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
            
            var harmony = new Harmony(nameof(PH_StudioPOV));
            harmony.PatchAll(typeof(PH_StudioPOV));
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
                rotationX += Input.GetAxis("Mouse X") * sensitivity.Value * Time.deltaTime;
                rotationY += Input.GetAxis("Mouse Y") * sensitivity.Value * Time.deltaTime;
                
                head.transform.localEulerAngles = new Vector3(-rotationY, rotationX, 0);
            }
            
            StartCoroutine(ApplyPOV());
        }

        private static IEnumerator ApplyPOV()
        {
            yield return new WaitForEndOfFrame();
            
            cc.targetPos = Vector3.Lerp(eyes[0].position, eyes[1].position, 0.5f);
            cc.cameraAngle = eyes[0].eulerAngles;
        }
        
        private static void StartPOV()
        {
            var ctrlInfo = Studio.Studio.GetCtrlInfo(studio.treeNodeCtrl.selectNode);
            if (!(ctrlInfo is OCIChar ocichar))
                return;

            chara = ocichar.charInfo;
            eyes = new Transform[2];

            head = chara.human.head.Obj;
            if (head == null)
                return;
            
            foreach (var child in head.transform.GetComponentsInChildren<Transform>())
            {
                if(child.name.Contains("_J_Eye_t_L"))
                    eyes[0] = child;
                else if(child.name.Contains("_J_Eye_t_R"))
                    eyes[1] = child;
            }
            
            if (eyes[0] == null || eyes[1] == null)
                return;

            if(hideHead.Value)
                head.SetActive(false);

            var data = cc.Export();
            
            backupData = data;
            backupFov = cc.fieldOfView;
            
            cc.Import(new CameraControl.CameraData(data) {distance = Vector3.zero});
            
            rotationX = 0f;
            rotationY = 0f;

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

            if (head != null)
            {
                head.transform.localEulerAngles = Vector3.zero;
                head.SetActive(true);
            }
            
            chara = null;
            eyes = null;
            head = null;
            backupData = null;
            toggle = false;
        }
        
        [HarmonyPrefix, HarmonyPatch(typeof(CameraControl), "LateUpdate")]
        private static bool CameraControl_LateUpdate_Patch(CameraControl __instance)
        {
            if (!toggle) 
                return true;
            
            Traverse.Create(__instance).Method("CameraUpdate").GetValue();
            
            return false;
        }
    }
}