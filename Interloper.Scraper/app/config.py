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

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"


settings = Settings()
