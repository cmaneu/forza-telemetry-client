using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForzaBridge.Model
{
    public class Session
    {
        public string SessionId { get; private set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Email { get; set; }
        public string Telephone { get; set; }
        public float BestLap { get; set; }

        public Session(string name, string email, string telephone)
        {
            Name = name;
            Email = email;
            Telephone = telephone;
            BestLap = 0;
        }
    }
}
