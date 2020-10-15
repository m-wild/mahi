using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mahi
{
    public abstract class Job
    {
        public Guid JobId { get; } = Guid.NewGuid();
    }

    public interface IJobProcessor<in T> where T : Job
    {
        Task ProcessAsync(T job, CancellationToken cancel = default);
    }

    public interface IJobQueue
    {
        Task EnqueueAsync(Job job);
    }

    public static class MahiServiceCollectionExtensions
    {
        public static IMahiBuilder AddMahi(this IServiceCollection services)
        {
            services.AddSingleton<BlockingCollectionJobQueue>();
            services.AddSingleton<IJobQueue>(sp => sp.GetRequiredService<BlockingCollectionJobQueue>());
            
            services.AddSingleton<JobProcessorHost>();
            
            services.AddHostedService<BlockingCollectionJobWorker>();
            
            return new MahiBuilder(services);
        }

        public static IMahiBuilder AddProcessor<TJob, TProcessor>(this IMahiBuilder builder) 
            where TJob : Job
            where TProcessor : class, IJobProcessor<TJob>

        {
            ((MahiBuilder) builder).services.AddScoped<IJobProcessor<TJob>, TProcessor>();
            return builder;
        }
        
        
        private class MahiBuilder : IMahiBuilder
        {
            internal readonly IServiceCollection services;

            public MahiBuilder(IServiceCollection services)
            {
                this.services = services;
            }
        }
    }

    public interface IMahiBuilder
    {
    }


    internal class BlockingCollectionJobQueue : IJobQueue
    {
        private readonly BlockingCollection<Job> jobs = new BlockingCollection<Job>();
        
        public Task EnqueueAsync(Job job)
        {
            jobs.Add(job);
            return Task.CompletedTask;
        }

        public Job Dequeue(CancellationToken cancel) => jobs.Take(cancel);
    }


    internal class BlockingCollectionJobWorker : IHostedService
    {
        private readonly BlockingCollectionJobQueue queue;
        private readonly JobProcessorHost processorHost;
        private readonly CancellationTokenSource cts;
        private readonly ILogger logger;

        private Task dequeueTask;

        public BlockingCollectionJobWorker(BlockingCollectionJobQueue queue,
            JobProcessorHost processorHost,
            ILoggerFactory loggerFactory)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
            this.processorHost = processorHost ?? throw new ArgumentNullException(nameof(processorHost));
            this.logger = loggerFactory?.CreateLogger("Mahi") ?? throw new ArgumentNullException(nameof(loggerFactory));
            this.cts = new CancellationTokenSource();
        }
        
        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.dequeueTask = Task.Run(Dequeue, cts.Token);
            
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            cts.Cancel(); // Signal stop
            await this.dequeueTask; // Wait for graceful finish
        }

        private async Task Dequeue()
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var job = queue.Dequeue(cts.Token);
                    await processorHost.ProcessAsync(job, cts.Token);
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing job: {Error}", ex.Message);
                }
            }
        }
    }

    internal class JobProcessorHost
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger logger;
        private readonly MethodInfo processInternalAsync;

        public JobProcessorHost(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            this.logger = loggerFactory?.CreateLogger("Mahi") ?? throw new ArgumentNullException(nameof(loggerFactory));
            
            this.processInternalAsync = this.GetType().GetMethod(nameof(ProcessInternalAsync), BindingFlags.Instance | BindingFlags.NonPublic) 
                                   ?? throw new Exception("ProcessInternalAsync method not found");
        }
        
        public async Task ProcessAsync(Job job, CancellationToken cancel)
        {
            _ = job ?? throw new ArgumentNullException(nameof(job));
            
            using (logger.BeginScope(new Dictionary<string, object> { ["JobId"] = job.JobId }))
            {
                logger.LogDebug("JOB {JobId} processing", job.JobId);

                // Need to use reflection to call the generic method based on the runtime type
                var jobType = job.GetType();
                var genericProcess = processInternalAsync.MakeGenericMethod(jobType);

                try
                {
                    await (Task) genericProcess.Invoke(this, new object[] { job, cancel });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "JOB {JobId} failed", job.JobId);
                }
            }
        }
        
        private async Task ProcessInternalAsync<T>(T job, CancellationToken cancel) where T : Job
        {
            using var scope = serviceProvider.CreateScope();
            
            var processor = scope.ServiceProvider.GetRequiredService<IJobProcessor<T>>();
            await processor.ProcessAsync(job, cancel);
        }

    }
    
    
}
