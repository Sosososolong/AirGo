using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.Utils.Constants;

namespace Sylas.RemoteTasks.App.Controllers
{
    [Authorize(Policy = AuthorizationConstants.AdministrationPolicy)]
    [ServiceFilter<MvcParameterFilter>]

    public class CustomBaseController : Controller
    {
    }
}
