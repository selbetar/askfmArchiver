using System.Threading.Tasks;
using askfmArchiver.Enums;

namespace askfmArchiver.Utils
{
    public interface IFileManager
    {
        void CheckDir(string dir);
        string ComputeHash(string file);
        Task<bool> SaveData<T>(T data, string file, FileType type);
    }
}