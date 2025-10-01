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
   * Clean text by removing HTML tags, emoticons, and other non-text content
   * @param {string} text - Raw text that may contain HTML, emoticons, etc.
   * @returns {string} Cleaned text suitable for TTS
   */
  cleanTextForTTS(text) {
    if (!text || typeof text !== 'string') {
      return ''
    }

    let cleanText = text

    // Remove HTML tags
    cleanText = cleanText.replace(/<[^>]*>/g, ' ')

    // Remove common markdown syntax
    cleanText = cleanText.replace(/\*\*(.*?)\*\*/g, '$1') // Bold **text**
    cleanText = cleanText.replace(/\*(.*?)\*/g, '$1') // Italic *text*
    cleanText = cleanText.replace(/`([^`]+)`/g, '$1') // Inline code `text`
    cleanText = cleanText.replace(/```[\s\S]*?```/g, '') // Code blocks
    cleanText = cleanText.replace(/#{1,6}\s*(.*?)$/gm, '$1') // Headers

    // Remove URLs
    cleanText = cleanText.replace(/https?:\/\/[^\s]+/g, '')

    // Remove emoticons and emojis (Unicode ranges)
    cleanText = cleanText.replace(/[\u{1F600}-\u{1F64F}]/gu, '') // Emoticons
    cleanText = cleanText.replace(/[\u{1F300}-\u{1F5FF}]/gu, '') // Misc Symbols
    cleanText = cleanText.replace(/[\u{1F680}-\u{1F6FF}]/gu, '') // Transport
    cleanText = cleanText.replace(/[\u{1F1E0}-\u{1F1FF}]/gu, '') // Flags
    cleanText = cleanText.replace(/[\u{2600}-\u{26FF}]/gu, '')   // Misc symbols
    cleanText = cleanText.replace(/[\u{2700}-\u{27BF}]/gu, '')   // Dingbats

    // Remove special characters and symbols commonly used in chat
    cleanText = cleanText.replace(/[📋📄📝📎🔊⏸️🔄]/g, '')

    // Replace HTML entities
    cleanText = cleanText.replace(/&nbsp;/g, ' ')
    cleanText = cleanText.replace(/&amp;/g, '&')
    cleanText = cleanText.replace(/&lt;/g, '<')
    cleanText = cleanText.replace(/&gt;/g, '>')
    cleanText = cleanText.replace(/&quot;/g, '"')
    cleanText = cleanText.replace(/&#39;/g, "'")

    // Clean up whitespace
    cleanText = cleanText.replace(/\s+/g, ' ') // Multiple spaces to single space
    cleanText = cleanText.replace(/\n+/g, '. ') // Line breaks to periods
    cleanText = cleanText.trim()

    // Remove empty parentheses, brackets, etc. that might be left over
    cleanText = cleanText.replace(/\(\s*\)/g, '')
    cleanText = cleanText.replace(/\[\s*\]/g, '')
    cleanText = cleanText.replace(/\{\s*\}/g, '')

    return cleanText
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
      // Clean the text before sending to API
      const cleanedText = this.cleanTextForTTS(text)
      
      // Don't generate audio for empty or very short text
      if (!cleanedText || cleanedText.length < 3) {
        const emptyEntry = {
          audioUrl: null,
          isLoading: false,
          error: 'No readable text content'
        }
        this.audioCache.set(messageId, emptyEntry)
        return emptyEntry
      }

      const apiUrl = buildApiUrl('api/texttospeech/speak')
      
      const response = await fetch(apiUrl, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          Text: cleanedText
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