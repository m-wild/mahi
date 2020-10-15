using System.Collections.Generic;
using System.Linq;

namespace StreetlightsAPI
{
    public interface IDatabase
    {
        IQueryable<Streetlight> Streetlights { get; }
        
        void Add(Streetlight streetlight);

        void Update(Streetlight streetlight);
    }
    
    public class Database : IDatabase
    {
        // Simulate a database of streetlights
        private static int StreetlightSeq = 2;

        private static readonly List<Streetlight> StreetlightDatabase = new List<Streetlight>
        {
            new Streetlight { Id = 1, Position = new[] { -36.320320, 175.485986 }, Lumens = 223 },
        };


        public IQueryable<Streetlight> Streetlights => StreetlightDatabase.AsQueryable();

        public void Add(Streetlight streetlight)
        {
            streetlight.Id = StreetlightSeq++;
            StreetlightDatabase.Add(streetlight);
        }

        public void Update(Streetlight streetlight)
        {
            var existing = StreetlightDatabase.Single(s => s.Id == streetlight.Id);
            existing.Lumens = streetlight.Lumens;
            existing.Position = streetlight.Position;
        }
    }
}