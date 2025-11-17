package org.example.storage

class InMemoryKeyValueStorage : KeyValueStorage {

    private val map = mutableMapOf<String, Any>()

    @Synchronized
    override fun save(key: String, value: Any) {
        map[key] = value
    }

    @Synchronized
    override fun get(key: String): Any? = map[key]
}

