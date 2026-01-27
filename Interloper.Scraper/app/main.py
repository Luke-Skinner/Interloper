import asyncio
import logging
from contextlib import asynccontextmanager
from decimal import Decimal

from fastapi import FastAPI, HTTPException

from app.cache import cache
from app.config import settings
from app.models import HealthResponse, SearchRequest, SearchResponse
from app.scrapers import scraper_registry

# Configure logging
logging.basicConfig(
    level=logging.DEBUG if settings.debug else logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan handler."""
    logger.info(f"Starting {settings.app_name} v{settings.app_version}")
    logger.info(f"Available platforms: {scraper_registry.get_platforms()}")

    # Connect to Redis cache
    await cache.connect()
    if cache.is_connected:
        logger.info("Redis cache enabled")
    else:
        logger.warning("Redis cache disabled - running without caching")

    yield

    # Cleanup
    logger.info("Shutting down...")
    await cache.disconnect()
    await scraper_registry.close_all()


app = FastAPI(
    title=settings.app_name,
    version=settings.app_version,
    lifespan=lifespan,
)


@app.get("/health", response_model=HealthResponse)
async def health_check():
    """Health check endpoint."""
    return HealthResponse(
        status="healthy",
        version=settings.app_version,
        platforms_available=scraper_registry.get_platforms(),
    )


@app.post("/search", response_model=SearchResponse)
async def search_hotels(request: SearchRequest):
    """
    Search for hotels across multiple platforms.

    Returns hotels matching the search criteria from all available scrapers.
    """
    logger.info(
        f"Search request: {request.city}, "
        f"{request.check_in} to {request.check_out}, "
        f"{request.guests} guests"
    )

    # Determine which scrapers to use
    if request.platforms:
        scrapers = [
            scraper_registry.get(p)
            for p in request.platforms
            if scraper_registry.get(p)
        ]
        if not scrapers:
            raise HTTPException(
                status_code=400,
                detail=f"No valid platforms specified. Available: {scraper_registry.get_platforms()}",
            )
    else:
        scrapers = scraper_registry.get_all()

    platforms_searched = [s.platform_name for s in scrapers]
    all_results = []
    errors = []

    # Run scrapers concurrently with semaphore for rate limiting
    semaphore = asyncio.Semaphore(settings.max_concurrent_requests)

    async def run_scraper(scraper):
        async with semaphore:
            try:
                results = await scraper.search(
                    city=request.city,
                    check_in=request.check_in,
                    check_out=request.check_out,
                    guests=request.guests,
                    hotel_name=request.hotel_name,
                    max_price=request.max_price,
                    min_rating=request.min_rating,
                    free_cancellation=request.free_cancellation,
                )
                return results
            except Exception as e:
                logger.error(f"Scraper {scraper.platform_name} failed: {e}")
                errors.append(f"{scraper.platform_name}: {str(e)}")
                return []

    # Run all scrapers
    tasks = [run_scraper(s) for s in scrapers]
    results_lists = await asyncio.gather(*tasks)

    for results in results_lists:
        all_results.extend(results)

    # Sort by price (put hotels without price at the end)
    all_results.sort(key=lambda x: (x.price is None, x.price or Decimal("999999")))

    logger.info(f"Search complete: {len(all_results)} results from {platforms_searched}")

    return SearchResponse(
        success=len(errors) == 0 or len(all_results) > 0,
        hotels=all_results,
        total_results=len(all_results),
        platforms_searched=platforms_searched,
        error_message="; ".join(errors) if errors else None,
    )


@app.post("/search/{platform}", response_model=SearchResponse)
async def search_single_platform(platform: str, request: SearchRequest):
    """Search a specific platform for hotels."""
    scraper = scraper_registry.get(platform)
    if not scraper:
        raise HTTPException(
            status_code=404,
            detail=f"Platform '{platform}' not found. Available: {scraper_registry.get_platforms()}",
        )

    try:
        results = await scraper.search(
            city=request.city,
            check_in=request.check_in,
            check_out=request.check_out,
            guests=request.guests,
            hotel_name=request.hotel_name,
            max_price=request.max_price,
            min_rating=request.min_rating,
            free_cancellation=request.free_cancellation,
        )

        results.sort(key=lambda x: x.price)

        return SearchResponse(
            success=True,
            hotels=results,
            total_results=len(results),
            platforms_searched=[platform],
        )

    except Exception as e:
        logger.error(f"Search failed for {platform}: {e}")
        return SearchResponse(
            success=False,
            hotels=[],
            total_results=0,
            platforms_searched=[platform],
            error_message=str(e),
        )
