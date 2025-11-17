package org.example

import org.example.storage.InMemoryKeyValueStorage
import org.example.storage.KeyValueStorage


class Leader(val keyValueStorage: KeyValueStorage, val replicatesUrls: Set<String>, val confirmationNeeded: Int) {
    init {
        require (replicatesUrls.size >= confirmationNeeded)
    }

    fun startHandling() {

    }

}

fun main(args: Array<String>) {

    val storage: KeyValueStorage = InMemoryKeyValueStorage()

    val confirmationNeeded = System.getenv("CONFIRMATIONS_NEEDED")?.toIntOrNull() ?: 10

    val urls = args.map { s -> "localhost:${s}"}.toSet()

    val leader = Leader(storage, urls, confirmationNeeded)

}
