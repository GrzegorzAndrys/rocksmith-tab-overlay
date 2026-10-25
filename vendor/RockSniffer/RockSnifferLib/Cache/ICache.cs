using RockSnifferLib.Sniffing;
using System.Collections.Generic;

namespace RockSnifferLib.Cache
{
    public interface ICache
    {
        /// <summary>
        /// Checks if the cache contains a cached version of a file
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        bool Contains(string filepath, string fileHash);

        /// <summary>
        /// Loads a cached version of a file
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        SongDetails Get(string filepath, string songID);

        /// <summary>
        /// Gets cached song details by searching the cache
        /// </summary>
        /// <param name="SongID"></param>
        /// <returns></returns>
        SongDetails Get(string SongID);

        /// <summary>Gets cached song details by an arrangement persistent ID.</summary>
        SongDetails GetByArrangementId(string arrangementID);

        /// <summary>
        /// Adds songdetails of a file to the cache
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="details"></param>
        void Add(string filepath, Dictionary<string, SongDetails> allDetails);

        /// <summary>
        /// Remove all details of a filepath and all song IDs from the cache
        /// </summary>
        /// <param name="filepath"></param>
        void Remove(string filepath, List<string> songIDs);
    }
}
