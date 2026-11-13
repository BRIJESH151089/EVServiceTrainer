using EVTrainer.Core;
using UnityEngine;

namespace EVTrainer.App
{
    /// <summary>
    /// Log routing per build type, before the first scene loads. Editor and Development builds: "[EVTrainer]" info
    /// logs go to the engine log. Release builds: info logs are compiled out (<see cref="DevLog"/>) and the engine
    /// log only passes warnings and errors (packages included).
    /// </summary>
    public static class BuildLogging
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            if (Debug.isDebugBuild)
            {
                DevLog.Sink = Debug.Log;
                Debug.unityLogger.filterLogType = LogType.Log;
            }
            else
            {
                DevLog.Sink = null;
                Debug.unityLogger.filterLogType = LogType.Warning;
            }
        }
    }
}
