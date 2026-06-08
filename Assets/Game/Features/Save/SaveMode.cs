namespace Save
{
    public enum SaveMode
    {
        Regular,        // debounce + push на сервер если онлайн
        ForceLocalOnly, // мгновенный flush, сервер не трогаем
        ForceWithSync   // мгновенный flush + push, минуя debounce и rate limit
    }
}
