namespace AtomicRegistry.Configuration
{
    public static class DirectoryHelper
    {
        public static void EnsurePathDirectoriesExist(string filePath)
        {
            if (File.Exists(filePath))
                return;

            var directoryPath = Directory.GetParent(filePath)!.FullName;
            Directory.CreateDirectory(directoryPath);
        }
    }
}