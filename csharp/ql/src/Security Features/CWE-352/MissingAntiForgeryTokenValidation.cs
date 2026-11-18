using System.Web.Mvc;

public class HomeController : Controller
{
    // GOOD: Anti forgery token is validated
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Login()
    {
        return View();
    }

    // GOOD: Anti forgery token is validated
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult UpdateDetails()
    {
        return View();
    }
}
