from datetime import date
from decimal import Decimal
from typing import Optional

from pydantic import BaseModel, Field


class SearchRequest(BaseModel):
    """Request model for hotel searches."""

    city: str = Field(..., description="City to search in")
    check_in: date = Field(..., description="Check-in date")
    check_out: date = Field(..., description="Check-out date")
    guests: int = Field(default=2, ge=1, le=10, description="Number of guests")
    hotel_name: Optional[str] = Field(
        default=None, description="Specific hotel name to search for"
    )
    max_price: Optional[Decimal] = Field(
        default=None, description="Maximum price per night filter"
    )
    min_rating: Optional[Decimal] = Field(
        default=None, ge=0, le=5, description="Minimum rating filter"
    )
    free_cancellation: bool = Field(
        default=False, description="Filter for free cancellation only"
    )
    platforms: Optional[list[str]] = Field(
        default=None, description="Specific platforms to search (default: all)"
    )


class HotelResult(BaseModel):
    """A single hotel result from scraping."""

    platform: str = Field(..., description="Source platform (e.g., 'booking')")
    hotel_id: str = Field(..., description="Platform-specific hotel ID")
    name: str = Field(..., description="Hotel name")
    price: Decimal = Field(..., description="Price per night")
    currency: str = Field(default="USD", description="Currency code")
    total_price: Optional[Decimal] = Field(
        default=None, description="Total price for the stay"
    )
    rating: Optional[Decimal] = Field(default=None, description="Rating (0-5 or 0-10)")
    review_count: Optional[int] = Field(default=None, description="Number of reviews")
    address: Optional[str] = Field(default=None, description="Hotel address")
    city: Optional[str] = Field(default=None, description="City")
    room_type: Optional[str] = Field(default=None, description="Room type description")
    amenities: Optional[list[str]] = Field(default=None, description="List of amenities")
    cancellation_policy: Optional[str] = Field(
        default=None, description="Cancellation policy"
    )
    breakfast_included: bool = Field(default=False, description="Breakfast included")
    booking_url: Optional[str] = Field(default=None, description="Direct booking URL")
    image_url: Optional[str] = Field(default=None, description="Hotel image URL")


class SearchResponse(BaseModel):
    """Response model for hotel searches."""

    success: bool = Field(..., description="Whether the search succeeded")
    hotels: list[HotelResult] = Field(
        default_factory=list, description="List of hotel results"
    )
    total_results: int = Field(default=0, description="Total number of results")
    platforms_searched: list[str] = Field(
        default_factory=list, description="Platforms that were searched"
    )
    error_message: Optional[str] = Field(
        default=None, description="Error message if search failed"
    )


class HealthResponse(BaseModel):
    """Health check response."""

    status: str = Field(..., description="Service status")
    version: str = Field(..., description="API version")
    platforms_available: list[str] = Field(
        default_factory=list, description="Available scraper platforms"
    )
