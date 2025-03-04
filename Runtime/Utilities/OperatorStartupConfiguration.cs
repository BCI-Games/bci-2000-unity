using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BCI2000
{
    [Serializable]
    public class OperatorStartupConfiguration
    {
        public bool RequiresSetup =>
            StartModulesWithConnection
            || !Parameters.IsNullOrEmpty()
            || !States.IsNullOrEmpty()
            || !Events.IsNullOrEmpty();

        public bool ResetSystemWhenConnected = false;

        [Header("Modules")]
        public bool StartModulesWithConnection = true;
        public ModuleConfiguration SignalSourceModule = new("SignalGenerator");
        public ModuleConfiguration SignalProcessingModule = new("DummySignalProcessing");
        public ModuleConfiguration ApplicationModule = new("DummyApplication");
        public ModuleConfiguration[] OtherModules;


        [Header("Parameters")]
        public ParameterDefinition[] Parameters;
        public string[] ParameterFiles;

        [Header("States")]
        public StateDefinition[] States;

        [Header("Events")]
        public EventDefinition[] Events;


        public void ForEachParameter(
            Action<string, string, string, string, string> action
        )
        => Array.ForEach(Parameters, p => p.DeconstructInto(action));

        public void ForEachState(Action<string, int, uint> action)
        => Array.ForEach(States, s => s.DeconstructInto(action));

        public void ForEachEvent(Action<string, int, uint> action)
        => Array.ForEach(Events, e => e.DeconstructInto(action));

        public void ForEachParameterFile(Action<string> action)
        => Array.ForEach(ParameterFiles, action);


        public Dictionary<string, IEnumerable<string>> GetModuleDictionary()
        {
            Dictionary<string, IEnumerable<string>> moduleDictionary = new();
            ModuleConfiguration[] moduleSet = new[] {
                SignalSourceModule, SignalProcessingModule, ApplicationModule
            };

            foreach(var (name, arguments) in moduleSet.Concat(OtherModules))
                moduleDictionary.Add(name, arguments);

            return moduleDictionary;
        }
    }

    public static class ArrayExtensions
    {
        public static bool IsNullOrEmpty(this Array ar)
        => ar is null or {Length: 0};
    }
}