using EPiServer.Commerce.Catalog.Linking;
using EPiServer.Core;
using EPiServer.Framework.Cache;
using EPiServer.Reference.Commerce.Site.Features.Product.Models;
using EPiServer.Reference.Commerce.Site.Features.Product.ViewModelFactories;
using EPiServer.Reference.Commerce.Site.Infrastructure.Facades;
using EPiServer.Web.Mvc;
using Mediachase.Commerce.Catalog;
using Mediachase.Commerce.Core;
using Mediachase.Commerce.Pricing;
using Mediachase.Commerce.Pricing.Database;
using System.Collections.Generic;
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


        public ProductController(IsInEditModeAccessor isInEditModeAccessor,
            CatalogEntryViewModelFactory viewModelFactory)
        {
            _isInEditMode = isInEditModeAccessor();
            _viewModelFactory = viewModelFactory;
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

            var contentLoader = ServiceLocation.ServiceLocator.Current.GetInstance<IContentLoader>();
            var priceService = ServiceLocation.ServiceLocator.Current.GetInstance<PriceServiceDatabase>();
            var relationRepository = ServiceLocation.ServiceLocator.Current.GetInstance<IRelationRepository>();
            var referenceConverter = ServiceLocation.ServiceLocator.Current.GetInstance<ReferenceConverter>();

            var variantLinks = relationRepository.GetChildren<ProductVariation>(currentContent.ContentLink);
            var variantCodes = new List<string>();
            foreach (var variantLink in variantLinks)
            {
                var variant = contentLoader.Get<FashionVariant>(variantLink.Child);
                variantCodes.Add(variant.Code);
            }

            var allPrices = new List<IPriceValue>();
            foreach (var code in variantCodes)
            {
                var prices = priceService.GetCatalogEntryPrices(new CatalogKey(code));
                allPrices.AddRange(prices);
            }

            var lowestPrice = allPrices.Where(x => x.UnitPrice.Currency == currency).OrderBy(x => x.UnitPrice).First();
            var highestPrice = allPrices.Where(x => x.UnitPrice.Currency == currency).OrderByDescending(x => x.UnitPrice).First();

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