from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    """Application settings loaded from environment variables."""

    app_name: str = "Interloper Scraper"
    app_version: str = "1.0.0"
    debug: bool = False

    # Rate limiting
    request_delay_seconds: float = 1.0
    max_concurrent_requests: int = 3

    # Timeouts
    request_timeout_seconds: int = 30

    # RapidAPI Configuration
    rapidapi_key: str = ""

    # Redis Configuration
    redis_url: str = "redis://localhost:6379"

    # Cache TTLs (in seconds)
    cache_destination_ttl: int = 86400  # 24 hours - destinations rarely change
    cache_search_ttl: int = 300  # 5 minutes - prices change frequently

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"
        extra = "ignore"


settings = Settings()
