namespace PropertyHistoryTool
{
    /// <summary>
    /// Contains information about a Git commit and the property value at that commit
    /// </summary>
    public class CommitInfo
    {
        /// <summary>
        /// The Git commit hash
        /// </summary>
        public string Hash { get; set; }
        
        /// <summary>
        /// The author of the commit
        /// </summary>
        public string Author { get; set; }
        
        /// <summary>
        /// The commit message
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// The property value at this commit
        /// </summary>
        public object Value { get; set; }
        
        /// <summary>
        /// Creates a CommitInfo instance
        /// </summary>
        public CommitInfo(string hash, string author, string message, object value)
        {
            Hash = hash;
            Author = author;
            Message = message;
            Value = value;
        }
        
        /// <summary>
        /// Returns a short version of the commit hash (first 7 characters)
        /// </summary>
        public string ShortHash => Hash?.Substring(0, System.Math.Min(7, Hash?.Length ?? 0)) ?? "";
    }
}