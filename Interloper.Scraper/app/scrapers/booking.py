import logging
import re
from datetime import date
from decimal import Decimal
from typing import Optional
from urllib.parse import quote

from bs4 import BeautifulSoup

from app.models import HotelResult

from .base import BaseScraper

logger = logging.getLogger(__name__)


class BookingScraper(BaseScraper):
    """Scraper for Booking.com."""

    platform_name = "booking"
    base_url = "https://www.booking.com"

    def _build_search_url(
        self,
        city: str,
        check_in: date,
        check_out: date,
        guests: int,
    ) -> str:
        """Build the Booking.com search URL."""
        # URL encode the city name
        dest = quote(city)

        url = (
            f"{self.base_url}/searchresults.html"
            f"?ss={dest}"
            f"&checkin={check_in.isoformat()}"
            f"&checkout={check_out.isoformat()}"
            f"&group_adults={guests}"
            f"&no_rooms=1"
            f"&group_children=0"
            f"&selected_currency=USD"
        )
        return url

    async def search(
        self,
        city: str,
        check_in: date,
        check_out: date,
        guests: int = 2,
        hotel_name: Optional[str] = None,
        max_price: Optional[Decimal] = None,
        min_rating: Optional[Decimal] = None,
        free_cancellation: bool = False,
    ) -> list[HotelResult]:
        """Search Booking.com for hotels."""
        results: list[HotelResult] = []

        try:
            url = self._build_search_url(city, check_in, check_out, guests)
            logger.info(f"Searching Booking.com: {city}")

            html = await self._fetch_page(url)
            soup = BeautifulSoup(html, "lxml")

            # Find hotel cards - Booking.com uses data-testid attributes
            hotel_cards = soup.select('[data-testid="property-card"]')

            if not hotel_cards:
                # Fallback selector for alternative page structure
                hotel_cards = soup.select(".sr_property_block")

            logger.debug(f"Found {len(hotel_cards)} hotel cards")

            for card in hotel_cards[:20]:  # Limit to first 20 results
                try:
                    hotel = self._parse_hotel_card(card, city, check_in, check_out)
                    if hotel:
                        # Apply filters
                        if hotel_name and hotel_name.lower() not in hotel.name.lower():
                            continue
                        if max_price and hotel.price > max_price:
                            continue
                        if min_rating and hotel.rating and hotel.rating < min_rating:
                            continue
                        if free_cancellation and "free cancellation" not in (
                            hotel.cancellation_policy or ""
                        ).lower():
                            continue

                        results.append(hotel)
                except Exception as e:
                    logger.warning(f"Failed to parse hotel card: {e}")
                    continue

        except Exception as e:
            logger.error(f"Booking.com search failed: {e}")

        return results

    def _parse_hotel_card(
        self,
        card: BeautifulSoup,
        city: str,
        check_in: date,
        check_out: date,
    ) -> Optional[HotelResult]:
        """Parse a single hotel card from search results."""
        try:
            # Hotel name
            name_elem = card.select_one('[data-testid="title"]')
            if not name_elem:
                name_elem = card.select_one(".sr-hotel__name")
            if not name_elem:
                return None

            name = name_elem.get_text(strip=True)

            # Price
            price_elem = card.select_one('[data-testid="price-and-discounted-price"]')
            if not price_elem:
                price_elem = card.select_one(".bui-price-display__value")
            if not price_elem:
                price_elem = card.select_one('[class*="price"]')

            price_text = price_elem.get_text(strip=True) if price_elem else ""
            price = self._extract_price(price_text)
            if not price:
                return None

            # Rating
            rating_elem = card.select_one('[data-testid="review-score"]')
            if not rating_elem:
                rating_elem = card.select_one(".bui-review-score__badge")

            rating = None
            if rating_elem:
                rating_text = rating_elem.get_text(strip=True)
                rating_match = re.search(r"(\d+\.?\d*)", rating_text)
                if rating_match:
                    raw_rating = float(rating_match.group(1))
                    # Booking uses 0-10 scale
                    rating = self._normalize_rating(raw_rating, max_rating=10.0)

            # Review count
            review_elem = card.select_one('[data-testid="review-score"] + span')
            review_count = None
            if review_elem:
                review_text = review_elem.get_text(strip=True)
                review_match = re.search(r"([\d,]+)\s*review", review_text, re.I)
                if review_match:
                    review_count = int(review_match.group(1).replace(",", ""))

            # Hotel link and ID
            link_elem = card.select_one('a[data-testid="title-link"]')
            if not link_elem:
                link_elem = card.select_one("a.hotel_name_link")

            booking_url = None
            hotel_id = "unknown"
            if link_elem and link_elem.get("href"):
                href = link_elem["href"]
                if not href.startswith("http"):
                    href = f"{self.base_url}{href}"
                booking_url = href

                # Extract hotel ID from URL
                id_match = re.search(r"/hotel/[^/]+/([^.?/]+)", href)
                if id_match:
                    hotel_id = id_match.group(1)

            # Address
            address_elem = card.select_one('[data-testid="address"]')
            address = address_elem.get_text(strip=True) if address_elem else None

            # Cancellation policy
            cancel_elem = card.select_one('[data-testid="cancellation-policy"]')
            cancellation = cancel_elem.get_text(strip=True) if cancel_elem else None

            # Calculate total nights
            nights = (check_out - check_in).days
            total_price = Decimal(str(price * nights)) if nights > 0 else price

            return HotelResult(
                platform=self.platform_name,
                hotel_id=hotel_id,
                name=name,
                price=price,
                currency="USD",
                total_price=total_price,
                rating=rating,
                review_count=review_count,
                address=address,
                city=city,
                cancellation_policy=cancellation,
                booking_url=booking_url,
            )

        except Exception as e:
            logger.debug(f"Error parsing hotel card: {e}")
            return None

    def _extract_price(self, price_text: str) -> Optional[Decimal]:
        """Extract numeric price from text."""
        if not price_text:
            return None

        # Remove currency symbols and whitespace
        cleaned = re.sub(r"[^\d.,]", "", price_text)

        # Handle European format (1.234,56) vs US format (1,234.56)
        if "," in cleaned and "." in cleaned:
            if cleaned.index(",") > cleaned.index("."):
                # European format
                cleaned = cleaned.replace(".", "").replace(",", ".")
            else:
                # US format
                cleaned = cleaned.replace(",", "")
        elif "," in cleaned:
            # Could be either format - assume US if 3 digits after comma
            parts = cleaned.split(",")
            if len(parts[-1]) == 3:
                cleaned = cleaned.replace(",", "")
            else:
                cleaned = cleaned.replace(",", ".")

        try:
            return Decimal(cleaned)
        except Exception:
            return None
