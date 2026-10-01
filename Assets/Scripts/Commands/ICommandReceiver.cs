namespace Commands
{
    public interface ICommandReceiver
    {
        void Handle(ICommand command);
    }
}