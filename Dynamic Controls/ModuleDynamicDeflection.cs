﻿﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using System.Reflection;

namespace Dynamic_Controls
{
    public class ModuleDynamicDeflection : DynamicModule
    {
        private bool usingFAR;

        public static ConfigNode defaults;
        public override ConfigNode defaultSetup
        {
            get { return defaults; }
            set { defaults = value; }
        }

        private FieldInfo farValToSet;

        public override string nodeName
        {
            get { return "DynamicDeflection"; }
        }

        public void Awake()
        {
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
                ModuleDynamicDeflection.defaults = GameDatabase.Instance.GetConfigNodes(nodeName).FirstOrDefault();
        }

        public void Start()
        {
            usingFAR = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name.Equals("FerramAerospaceResearch", StringComparison.InvariantCultureIgnoreCase));
            if (part.Modules.Contains("FARControllableSurface"))
                module = part.Modules["FARControllableSurface"];
            else
                module = part.Modules.OfType<ModuleControlSurface>().FirstOrDefault();

            if (usingFAR)
                farValToSet = module.GetType().GetField("maxdeflect");

            if (deflectionAtValue == null)
            {
                deflectionAtValue = new List<List<float>>();

                if (defaults == null)
                    defaults = new ConfigNode(nodeName);
                LoadConfig(defaults, true);
            }

            if (!loaded)
            {
                if (usingFAR)
                    deflection = (float)farValToSet.GetValue(module);
                else
                    deflection = ((ModuleControlSurface)module).ctrlSurfaceRange;
                loaded = true;
            }
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (usingFAR)
                    deflection = (float)farValToSet.GetValue(module);
                else
                    deflection = ((ModuleControlSurface)module).ctrlSurfaceRange;
            }

            if (windowInstance.moduleToDraw != this)
                return;

            foreach (Part p in part.symmetryCounterparts)
            {
                if (p != null)
                    copyToModule(p.Modules.OfType<ModuleDynamicDeflection>().FirstOrDefault(), deflectionAtValue);
            }
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel.HoldPhysics)
                return;

            currentDeflection = Mathf.Clamp(Evaluate(deflectionAtValue, (float)vessel.dynamicPressurekPa) * deflection, -89, 89);

            if (usingFAR)
                farValToSet.SetValue(module, currentDeflection);
            else
                ((ModuleControlSurface)module).ctrlSurfaceRange = currentDeflection;
        }

        public override void OnSave(ConfigNode node)
        {
            if (!loaded)
                return;
            try
            {
                node = EditorWindow.toConfigNode(deflectionAtValue, node, false, deflection);
                base.OnSave(node);
            }
            catch (Exception ex)
            {
                Log("Onsave failed");
                Log(ex.InnerException);
                Log(ex.StackTrace);
            }
        }

        // copy to every control surface on the vessel, not just the sym counterparts
        public override void copyToAll()
        {
            foreach (Part p in (HighLogic.LoadedSceneIsEditor ? EditorLogic.fetch.getSortedShipList() : vessel.parts))
            {
                if (p != null && p.Modules.Contains("ModuleDynamicDeflection"))
                    copyToModule(p.Modules.OfType<ModuleDynamicDeflection>().FirstOrDefault(), deflectionAtValue);
            }
        }

        public override void UpdateDefaults(ConfigNode node)
        {
            defaults = node;
        }
    }
}
