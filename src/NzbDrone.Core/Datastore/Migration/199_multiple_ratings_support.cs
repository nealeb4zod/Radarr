using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using Dapper;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(199)]
    public class multiple_ratings_support : NzbDroneMigrationBase
    {
        private readonly JsonSerializerOptions _serializerSettings;

        public multiple_ratings_support()
        {
            _serializerSettings = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                IgnoreNullValues = false,
                PropertyNameCaseInsensitive = true,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        protected override void MainDbUpgrade()
        {
            Execute.WithConnection((conn, tran) => FixRatings(conn, tran, "Movies"));
            Execute.WithConnection((conn, tran) => FixRatings(conn, tran, "ImportListMovies"));
        }

        private void FixRatings(IDbConnection conn, IDbTransaction tran, string table)
        {
            var rows = conn.Query<Movie198>($"SELECT Id, Ratings FROM {table}");

            var corrected = new List<Movie199>();

            foreach (var row in rows)
            {
                var oldRatings = JsonSerializer.Deserialize<Ratings198>(row.Ratings, _serializerSettings);

                var newRatings = new Ratings199
                {
                    Tmdb = new RatingChild199
                    {
                        Votes = oldRatings.Votes,
                        Value = oldRatings.Value,
                        Type = RatingType199.User
                    }
                };

                corrected.Add(new Movie199
                {
                    Id = row.Id,
                    Ratings = JsonSerializer.Serialize(newRatings, _serializerSettings)
                });
            }

            var updateSql = $"UPDATE {table} SET Ratings = @Ratings WHERE Id = @Id";
            conn.Execute(updateSql, corrected, transaction: tran);
        }

        private class Movie198
        {
            public int Id { get; set; }
            public string Ratings { get; set; }
        }

        private class Ratings198
        {
            public int Votes { get; set; }
            public decimal Value { get; set; }
        }

        private class Movie199
        {
            public int Id { get; set; }
            public string Ratings { get; set; }
        }

        private class Ratings199
        {
            public RatingChild199 Tmdb { get; set; }
            public RatingChild199 Imdb { get; set; }
            public RatingChild199 MetaCritic { get; set; }
            public RatingChild199 RottenTomatoes { get; set; }
        }

        private class RatingChild199
        {
            public int Votes { get; set; }
            public decimal Value { get; set; }
            public RatingType199 Type { get; set; }
        }

        private enum RatingType199
        {
            User
        }
    }
}
