
namespace Telescope
{
    public interface IPublicationParser
    {
        Task AddChanelToUser(string channelName, long userID);
    }

}
