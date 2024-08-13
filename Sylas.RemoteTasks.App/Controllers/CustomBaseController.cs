using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.Utils.Constants;

namespace Sylas.RemoteTasks.App.Controllers
{
    [Authorize(Policy = AuthorizationConstants.AdministrationPolicy)]
    public class CustomBaseController : Controller
    {
    }
}
