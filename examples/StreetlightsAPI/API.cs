using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mahi;
using Microsoft.AspNetCore.Mvc;

namespace StreetlightsAPI
{
    public class Streetlight
    {
        /// <summary>
        /// Id of the streetlight.
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Lat-Long coordinates of the streetlight.
        /// </summary>
        public double[] Position { get; set; }

        /// <summary>
        /// Light intensity measured in lumens.
        /// </summary>
        public long Lumens { get; set; }
    }

    public class SetBrightnessRequest
    {
        /// <summary>
        /// Desired light intensity measured in lumens.
        /// </summary>
        public long DesiredLumens { get; set; }
    }
    
    [ApiController]
    [Route("api/streetlights")]
    public class StreetlightsController
    {
       
        private readonly IJobQueue jobQueue;
        private readonly IDatabase database;

        public StreetlightsController(IJobQueue jobQueue, IDatabase database)
        {
            this.jobQueue = jobQueue;
            this.database = database;
        }

        /// <summary>
        /// Get all streetlights
        /// </summary>
        [HttpGet]
        public IEnumerable<Streetlight> Get() => database.Streetlights.ToList();


        /// <summary>
        /// Set brightness for a particular streetlight.
        /// </summary>
        [HttpPost]
        [Route("{id}/brightness")]
        public async Task<IActionResult> SetBrightness([FromRoute] int id, [FromBody] SetBrightnessRequest request)
        {
            var streetlight = database.Streetlights.Single(s => s.Id == id);

            // kick off background job to update the light.
            // perhaps this involves an unreliable connection to the streetlight control unit.  
            await jobQueue.EnqueueAsync(new SetBrightnessJob
            {
                StreetlightId = streetlight.Id,
                DesiredLumens = request.DesiredLumens,
            });

            return new AcceptedResult();
        }
    }
}