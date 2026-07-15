using Microsoft.Extensions.AI;

namespace Maxwell;

public interface IToolProxy
{
    AIFunction InvokeToolDelegate {get;}
}