using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mahi;
using Microsoft.Extensions.Logging;

namespace StreetlightsAPI
{
    public class SetBrightnessJob : Job
    {
        public int StreetlightId { get; set; }
        
        public long DesiredLumens { get; set; }
    }
    
    public class SetBrightnessJobProcessor : IJobProcessor<SetBrightnessJob>
    {
        private readonly IDatabase database;
        private readonly ILogger logger;

        public SetBrightnessJobProcessor(IDatabase database, ILoggerFactory loggerFactory)
        {
            this.database = database;
            this.logger = loggerFactory.CreateLogger<SetBrightnessJobProcessor>();
        }
        
        public async Task ProcessAsync(SetBrightnessJob job, CancellationToken cancel = default)
        {
            // Simulate some long-running operation to set the streetlight brightness.
            await Task.Delay(5000, cancel); 
            
            
            // Update the database
            var streetlight = database.Streetlights.Single(s => s.Id == job.StreetlightId);
            streetlight.Lumens = job.DesiredLumens;
            database.Update(streetlight);
            
            logger.LogInformation("Light {Id} intensity set to {lumens}", streetlight.Id, streetlight.Lumens);
        }
    }
}