using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D12;

namespace SimpleRHI.D3D12.Helpers
{
    internal interface ITransitionableResource
    {
        public ResourceStates CurrentState { get; }

        //why do i have an isPlacebo argument?
        //cause it transitions to a COMMON state when using a copy queue
        //so it needs to ignore that cause it will then get converted to either CopySource or CopyDest
        //also this should be commandbuffer agnositic as to simply the code and reduce bugs
        //im just to lazy to refactor as of writing
        //turns out this is wrong omg this is so fucked i really should refactor
        public void TransitionIfRequired(ID3D12GraphicsCommandList10 commandBuffer, ResourceStates newState, bool isPlacebo = false);
    }
}
