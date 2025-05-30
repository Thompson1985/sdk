﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Microsoft.Build.Execution;
using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch
{
    internal static class HotReloadProfileReader
    {
        public static HotReloadProfile InferHotReloadProfile(ProjectGraphNode projectNode, IReporter reporter)
        {
            if (projectNode.IsWebApp())
            {
                var queue = new Queue<ProjectGraphNode>();
                queue.Enqueue(projectNode);

                ProjectInstance? aspnetCoreProject = null;

                var visited = new HashSet<ProjectGraphNode>();

                while (queue.Count > 0)
                {
                    var currentNode = queue.Dequeue();
                    var projectCapability = currentNode.ProjectInstance.GetItems("ProjectCapability");

                    foreach (var item in projectCapability)
                    {
                        if (item.EvaluatedInclude == "AspNetCore")
                        {
                            aspnetCoreProject = currentNode.ProjectInstance;
                            break;
                        }

                        if (item.EvaluatedInclude == "WebAssembly")
                        {
                            // We saw a previous project that was AspNetCore. This must he a blazor hosted app.
                            if (aspnetCoreProject is not null && aspnetCoreProject != currentNode.ProjectInstance)
                            {
                                reporter.Verbose($"HotReloadProfile: BlazorHosted. {aspnetCoreProject.FullPath} references BlazorWebAssembly project {currentNode.ProjectInstance.FullPath}.", emoji: "🔥");
                                return HotReloadProfile.BlazorHosted;
                            }

                            reporter.Verbose("HotReloadProfile: BlazorWebAssembly.", emoji: "🔥");
                            return HotReloadProfile.BlazorWebAssembly;
                        }
                    }

                    foreach (var project in currentNode.ProjectReferences)
                    {
                        if (visited.Add(project))
                        {
                            queue.Enqueue(project);
                        }
                    }
                }
            }

            reporter.Verbose("HotReloadProfile: Default.", emoji: "🔥");
            return HotReloadProfile.Default;
        }
    }
}
