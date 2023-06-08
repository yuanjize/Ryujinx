using System;

namespace ChocolArm64.State
{   //系统调用ID
    public class SvcEventArgs : EventArgs
    {
        public int Id { get; private set; }

        public SvcEventArgs(int Id)
        {
            this.Id = Id;
        }
    }
}