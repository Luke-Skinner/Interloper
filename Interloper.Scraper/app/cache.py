"""Redis caching layer for the scraper service."""

import hashlib
import json
import logging
from typing import Optional

import redis.asyncio as redis

from app.config import settings

logger = logging.getLogger(__name__)


class CacheService:
    """Redis-based caching service for API responses."""

    def __init__(self):
        self._client: Optional[redis.Redis] = None

    async def connect(self) -> None:
        """Connect to Redis."""
        if not settings.redis_url:
            logger.warning("Redis URL not configured, caching disabled")
            return

        try:
            self._client = redis.from_url(
                settings.redis_url,
                encoding="utf-8",
                decode_responses=True,
            )
            # Test connection
            await self._client.ping()
            logger.info("Connected to Redis cache")
        except Exception as e:
            logger.error(f"Failed to connect to Redis: {e}")
            self._client = None

    async def disconnect(self) -> None:
        """Disconnect from Redis."""
        if self._client:
            await self._client.close()
            self._client = None
            logger.info("Disconnected from Redis cache")

    @property
    def is_connected(self) -> bool:
        """Check if Redis is connected."""
        return self._client is not None

    def _make_key(self, prefix: str, *args) -> str:
        """Generate a cache key from prefix and arguments."""
        key_data = ":".join(str(arg) for arg in args)
        # Hash long keys to keep them manageable
        if len(key_data) > 100:
            key_hash = hashlib.md5(key_data.encode()).hexdigest()[:16]
            return f"{prefix}:{key_hash}"
        return f"{prefix}:{key_data}"

    async def get(self, key: str) -> Optional[dict]:
        """Get a cached value."""
        if not self._client:
            return None

        try:
            data = await self._client.get(key)
            if data:
                logger.debug(f"Cache HIT: {key}")
                return json.loads(data)
            logger.debug(f"Cache MISS: {key}")
            return None
        except Exception as e:
            logger.warning(f"Cache get error: {e}")
            return None

    async def set(self, key: str, value: dict, ttl_seconds: int) -> bool:
        """Set a cached value with TTL."""
        if not self._client:
            return False

        try:
            await self._client.setex(key, ttl_seconds, json.dumps(value))
            logger.debug(f"Cache SET: {key} (TTL: {ttl_seconds}s)")
            return True
        except Exception as e:
            logger.warning(f"Cache set error: {e}")
            return False

    async def delete(self, key: str) -> bool:
        """Delete a cached value."""
        if not self._client:
            return False

        try:
            await self._client.delete(key)
            return True
        except Exception as e:
            logger.warning(f"Cache delete error: {e}")
            return False

    # Convenience methods for specific cache types

    async def get_destination(self, platform: str, city: str) -> Optional[dict]:
        """Get cached destination/region lookup."""
        key = self._make_key("dest", platform, city.lower())
        return await self.get(key)

    async def set_destination(self, platform: str, city: str, data: dict) -> bool:
        """Cache destination/region lookup (long TTL - rarely changes)."""
        key = self._make_key("dest", platform, city.lower())
        return await self.set(key, data, settings.cache_destination_ttl)

    async def get_search_results(
        self,
        platform: str,
        city: str,
        check_in: str,
        check_out: str,
        guests: int,
    ) -> Optional[list]:
        """Get cached search results."""
        key = self._make_key("search", platform, city.lower(), check_in, check_out, guests)
        return await self.get(key)

    async def set_search_results(
        self,
        platform: str,
        city: str,
        check_in: str,
        check_out: str,
        guests: int,
        results: list,
    ) -> bool:
        """Cache search results (short TTL - prices change frequently)."""
        key = self._make_key("search", platform, city.lower(), check_in, check_out, guests)
        return await self.set(key, results, settings.cache_search_ttl)


# Global cache instance
cache = CacheService()
