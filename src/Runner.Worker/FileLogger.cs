using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GitHub.DistributedTask.Pipelines.ContextData;
using GitHub.DistributedTask.WebApi;
using GitHub.Runner.Common;
using GitHub.Runner.Worker;
using Serilog;
using Serilog.Formatting.Json;
using YamlDotNet.Core.Tokens;

namespace Runner.Worker
{
    [ServiceLocator(Default = typeof(FileLogger))]
    public interface ILogger : IRunnerService
    {
        public void Log(string message, IExecutionContext executionContext);
    }
    public class FileLogger : RunnerService, ILogger
    {
        private Serilog.ILogger _logger;

        public override void Initialize(IHostContext hostContext)
        {
            base.Initialize(hostContext);

            var logFilePath =
                Path.Combine(hostContext.GetDirectory(WellKnownDirectory.Diag), "logs", "full_log.log");

            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));

            _logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(new JsonFormatter(), logFilePath, rollOnFileSizeLimit: true, retainedFileCountLimit: 20 )
                .CreateLogger();
        }

        public void Log(string message, IExecutionContext executionContext)
        {
            var actionsContext = new Dictionary<string, object>();
            var envContext = executionContext.ExpressionValues["env"];

            if (envContext is IEnumerable<KeyValuePair<string, PipelineContextData>> enumerable)
            {
                var envDict = new Dictionary<string, string>();
                foreach (var (key, value) in enumerable)
                {
                    envDict.TryAdd(
                        key.Replace("log_", "", StringComparison.OrdinalIgnoreCase),
                        HostContext.SecretMasker.MaskSecrets(value as StringContextData)
                    );
                }

                actionsContext.Add("env", envDict);
            }

            AddPair(actionsContext, "workflow_name", executionContext.GetGitHubContext("workflow"));
            AddPair(actionsContext, "job_name", executionContext.Root.Record.Name);
            AddPair(actionsContext, "step_name", executionContext.Record.Name);

            AddPair(actionsContext, "run_id", executionContext.GetGitHubContext("run_id"));
            AddPair(actionsContext, "run_number", executionContext.GetGitHubContext("run_number"));
            AddPair(actionsContext, "run_attempt", executionContext.GetGitHubContext("run_attempt"));
            AddPair(actionsContext, "repository", executionContext.GetGitHubContext("repository"));
            AddPair(actionsContext, "sha", executionContext.GetGitHubContext("sha"));
            AddPair(actionsContext, "ref", executionContext.GetGitHubContext("ref"));
            AddPair(actionsContext, "author", executionContext.GetGitHubContext("actor"));

            _logger.ForContext("actions", actionsContext, destructureObjects: true).Information(message);
        }

        private void AddPair(Dictionary<string, object> dict, string key, string value)
        {
            dict.Add(key, HostContext.SecretMasker.MaskSecrets(value));
        }
    }
}
