using System.Collections.Generic;
using UnityEngine;

namespace Commands
{
    public class CommandRouter
    {
        private readonly Dictionary<int, ICommandReceiver> _receivers =  new();

        public void RegisterReceiver(int id, ICommandReceiver receiver)
        {
            _receivers[id] = receiver;
        }

        public void RouteCommand(int id, ICommand command)
        {
            if (_receivers.TryGetValue(id, out var receiver))
            {
                receiver.Handle(command);
            }
            else
            {
                Debug.LogError($"Command receiver {id} not registered");
            }
        }

        public void RouteCommand(IAddresee addressee, ICommand command)
        {
            switch (addressee)
            {
                case Broadcast:
                    foreach (var receiver in _receivers.Values)
                    {
                        receiver.Handle(command);
                    }
                    break;
                case Individual individual:
                    RouteCommand(individual.Id, command);
                    break;
            }
        }
    }
}
