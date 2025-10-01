import { ref } from 'vue'
import { buildApiUrl } from '../utils/pathUtils.js'

class TextToSpeechService {
  constructor() {
    // Audio caching and state management
    this.audioCache = new Map() // messageId -> { audioUrl, isLoading, error }
    this.currentlyPlaying = ref(null) // messageId of currently playing audio
    this.audioElements = new Map() // messageId -> HTMLAudioElement
  }

  /**
   * Generate audio for given text and cache it
   * @param {string} messageId - Unique identifier for the message
   * @param {string} text - Text to convert to speech
   * @returns {Promise<Object>} Audio cache entry
   */
  async generateAudio(messageId, text) {
    // Check if already cached
    if (this.audioCache.has(messageId)) {
      return this.audioCache.get(messageId)
    }

    // Set loading state
    this.audioCache.set(messageId, { 
      audioUrl: null, 
      isLoading: true, 
      error: null 
    })

    try {
      const apiUrl = buildApiUrl('api/texttospeech/speak')
      
      const response = await fetch(apiUrl, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          Text: text
        })
      })

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`)
      }

      // Get audio blob from response
      const audioBlob = await response.blob()
      const audioUrl = URL.createObjectURL(audioBlob)

      // Cache the result
      const cacheEntry = {
        audioUrl,
        isLoading: false,
        error: null
      }
      this.audioCache.set(messageId, cacheEntry)
      
      return cacheEntry
    } catch (error) {
      console.error('Error generating audio:', error)
      const errorEntry = {
        audioUrl: null,
        isLoading: false,
        error: 'Failed to generate audio'
      }
      this.audioCache.set(messageId, errorEntry)
      return errorEntry
    }
  }

  /**
   * Play audio for a specific message
   * @param {string} messageId - Message identifier
   * @param {string} text - Text to convert to speech
   */
  async playAudio(messageId, text) {
    try {
      // Stop any currently playing audio
      if (this.currentlyPlaying.value && this.currentlyPlaying.value !== messageId) {
        this.stopAudio(this.currentlyPlaying.value)
      }

      // Get or generate audio
      const audioData = await this.generateAudio(messageId, text)
      
      if (audioData.error) {
        console.error('Audio generation failed:', audioData.error)
        return
      }

      if (!audioData.audioUrl) {
        console.error('No audio URL available')
        return
      }

      // Create or get audio element
      let audioElement = this.audioElements.get(messageId)
      if (!audioElement) {
        audioElement = new Audio(audioData.audioUrl)
        this.audioElements.set(messageId, audioElement)
        
        // Add event listeners
        audioElement.addEventListener('ended', () => {
          this.currentlyPlaying.value = null
        })
        
        audioElement.addEventListener('pause', () => {
          if (this.currentlyPlaying.value === messageId) {
            this.currentlyPlaying.value = null
          }
        })
      }

      // Play audio
      this.currentlyPlaying.value = messageId
      await audioElement.play()
    } catch (error) {
      console.error('Error playing audio:', error)
      this.currentlyPlaying.value = null
    }
  }

  /**
   * Stop audio for a specific message
   * @param {string} messageId - Message identifier
   */
  stopAudio(messageId) {
    const audioElement = this.audioElements.get(messageId)
    if (audioElement) {
      audioElement.pause()
      audioElement.currentTime = 0
    }
    if (this.currentlyPlaying.value === messageId) {
      this.currentlyPlaying.value = null
    }
  }

  /**
   * Toggle audio playback for a message
   * @param {string} messageId - Message identifier
   * @param {string} text - Text to convert to speech
   */
  toggleAudio(messageId, text) {
    if (this.currentlyPlaying.value === messageId) {
      this.stopAudio(messageId)
    } else {
      this.playAudio(messageId, text)
    }
  }

  /**
   * Get audio cache entry for a message
   * @param {string} messageId - Message identifier
   * @returns {Object|undefined} Cache entry or undefined
   */
  getAudioCache(messageId) {
    return this.audioCache.get(messageId)
  }

  /**
   * Check if audio is currently playing for a message
   * @param {string} messageId - Message identifier
   * @returns {boolean} True if playing
   */
  isPlaying(messageId) {
    return this.currentlyPlaying.value === messageId
  }

  /**
   * Get reactive reference to currently playing message ID
   * @returns {Ref} Vue ref to currently playing message ID
   */
  getCurrentlyPlayingRef() {
    return this.currentlyPlaying
  }

  /**
   * Clean up all audio resources
   * Call this when component unmounts to prevent memory leaks
   */
  cleanup() {
    // Clean up audio elements
    this.audioElements.forEach((audioElement) => {
      audioElement.pause()
      audioElement.src = ''
    })
    this.audioElements.clear()
    
    // Revoke cached audio URLs to prevent memory leaks
    this.audioCache.forEach((cacheEntry) => {
      if (cacheEntry.audioUrl) {
        URL.revokeObjectURL(cacheEntry.audioUrl)
      }
    })
    this.audioCache.clear()
    
    // Reset state
    this.currentlyPlaying.value = null
  }
}

// Export a singleton instance
export default new TextToSpeechService()