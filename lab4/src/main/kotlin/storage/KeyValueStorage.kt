package org.example.storage

interface KeyValueStorage {
    fun save(key: String, value: Any)
    fun get(key: String): Any?
}
