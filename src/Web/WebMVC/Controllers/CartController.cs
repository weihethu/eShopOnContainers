namespace Microsoft.eShopOnContainers.WebMVC.Controllers;

[Authorize]
public class CartController : Controller
{
    private readonly IBasketService _basketSvc;
    private readonly ICatalogService _catalogSvc;
    private readonly IIdentityParser<ApplicationUser> _appUserParser;
    private readonly IHttpClientFactory _clientFactory;

    public CartController(IBasketService basketSvc, ICatalogService catalogSvc, IIdentityParser<ApplicationUser> appUserParser, IHttpClientFactory clientFactory)
    {
        _basketSvc = basketSvc;
        _catalogSvc = catalogSvc;
        _appUserParser = appUserParser;
        _clientFactory = clientFactory;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var user = _appUserParser.Parse(HttpContext.User);
            var vm = await _basketSvc.GetBasket(user);

            return View(vm);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Index(Dictionary<string, int> quantities, string action)
    {
        try
        {
            var user = _appUserParser.Parse(HttpContext.User);
            var basket = await _basketSvc.SetQuantities(user, quantities);
            if (action == "[ Checkout ]")
            {
                return RedirectToAction("Create", "Order");
            }
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ApplyCoupon(string couponCode, List<Item> items)
    {
        try
        {
            var user = _appUserParser.Parse(HttpContext.User);
            var client = _clientFactory.CreateClient();
            var couponApplicationResult = await client.PostAsJsonAsync("http://localhost:5000/apply-coupon", new { couponCode, items });
            couponApplicationResult.EnsureSuccessStatusCode();
            var result = await couponApplicationResult.Content.ReadAsAsync<CouponApplicationResult>();

            if (result.Success)
            {
                // Update the basket with the new prices
                await _basketSvc.UpdateBasket(user, result.UpdatedItems);
            }
            else
            {
                // Handle the error message accordingly
                ModelState.AddModelError("", $"Coupon application failed: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> AddToCart(CatalogItem productDetails)
    {
        try
        {
            if (productDetails?.Id != null)
            {
                var user = _appUserParser.Parse(HttpContext.User);
                await _basketSvc.AddItemToBasket(user, productDetails.Id);
            }
            return RedirectToAction("Index", "Catalog");
        }
        catch (Exception ex)
        {
            // Catch error when Basket.api is in circuit-opened mode                 
            HandleException(ex);
        }

        return RedirectToAction("Index", "Catalog", new { errorMsg = ViewBag.BasketInoperativeMsg });
    }

    private void HandleException(Exception ex)
    {
        ViewBag.BasketInoperativeMsg = $"Basket Service is inoperative {ex.GetType().Name} - {ex.Message}";
    }
}

public class Item
{
    public string Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

public class CouponApplicationResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public List<Item> UpdatedItems { get; set; }
}
