using EPiServer.Commerce.Catalog.Linking;
using EPiServer.Core;
using EPiServer.Reference.Commerce.Site.Features.Product.Models;
using EPiServer.Reference.Commerce.Site.Features.Product.ViewModelFactories;
using EPiServer.Reference.Commerce.Site.Infrastructure.Facades;
using EPiServer.Web.Mvc;
using Mediachase.Commerce.Catalog;
using Mediachase.Commerce.Core;
using Mediachase.Commerce.Pricing;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace EPiServer.Reference.Commerce.Site.Features.Product.Controllers
{
    public class ProductController : ContentController<FashionProduct>
    {
        private readonly bool _isInEditMode;
        private readonly CatalogEntryViewModelFactory _viewModelFactory;
        private readonly IContentLoader _contentLoader;
        private readonly IPriceService _priceService;
        private readonly IRelationRepository _relationRepository;
        private readonly ReferenceConverter _referenceConverter;

        public ProductController(IsInEditModeAccessor isInEditModeAccessor,
            CatalogEntryViewModelFactory viewModelFactory,
            IContentLoader contentLoader,
            IPriceService priceService,
            IRelationRepository relationRepository,
            ReferenceConverter referenceConverter)
        {
            _isInEditMode = isInEditModeAccessor();
            _viewModelFactory = viewModelFactory;
            _contentLoader = contentLoader;
            _priceService = priceService;
            _relationRepository = relationRepository;
            _referenceConverter = referenceConverter;
        }

        [HttpGet]
        public ActionResult Index(FashionProduct currentContent, string entryCode = "", bool useQuickview = false, bool skipTracking = false)
        {
            //Clear cache
            var enumerator = HttpRuntime.Cache.GetEnumerator();
            while (enumerator.MoveNext())
            {
                HttpRuntime.Cache.Remove((string)enumerator.Key);
            }
            var currency = SiteContext.Current.Currency;

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            //--------------------------------------------
            //Start to review 
            var variantLinks = _relationRepository.GetChildren<ProductVariation>(currentContent.ContentLink);
            var variantCodes = variantLinks.Select(x => _referenceConverter.GetCode(x.Child));

            var allPrices = _priceService.GetCatalogEntryPrices(variantCodes.Select(x => new CatalogKey(x)))
                .Where(x => x.UnitPrice.Currency == currency).OrderBy(x => x.UnitPrice);

            var lowestPrice = allPrices.First();
            var highestPrice = allPrices.Last();

            //----------------------------------------------
            //End review

            stopWatch.Stop();
            var viewModel = _viewModelFactory.Create(currentContent, entryCode);
            viewModel.TimeSpent = $"Time spent {stopWatch.ElapsedMilliseconds} ms";
            viewModel.PriceRange = $"{lowestPrice.UnitPrice.ToString()} - {highestPrice.UnitPrice.ToString()}";
            viewModel.SkipTracking = skipTracking;

            if (_isInEditMode && viewModel.Variant == null)
            {
                var emptyViewName = "ProductWithoutEntries";
                return Request.IsAjaxRequest() ? PartialView(emptyViewName, viewModel) : (ActionResult)View(emptyViewName, viewModel);
            }

            if (viewModel.Variant == null)
            {
                return HttpNotFound();
            }

            if (useQuickview)
            {
                return PartialView("_Quickview", viewModel);
            }
            return Request.IsAjaxRequest() ? PartialView(viewModel) : (ActionResult)View(viewModel);
        }

        [HttpPost]
        public ActionResult SelectVariant(FashionProduct currentContent, string color, string size, bool useQuickview = false)
        {
            var variant = _viewModelFactory.SelectVariant(currentContent, color, size);
            if (variant != null)
            {
                return RedirectToAction("Index", new { entryCode = variant.Code, useQuickview, skipTracking = true });
            }

            return HttpNotFound();
        }
    }
}